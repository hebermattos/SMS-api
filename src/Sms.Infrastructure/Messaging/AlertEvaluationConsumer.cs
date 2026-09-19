using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertEvaluationConsumer(
    SqlConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<AlertEvaluationConsumer> logger) : BackgroundService
{
    private readonly RabbitMqAlertOptions options = RabbitMqAlertOptions.From(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Alert evaluation consumer stopped; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Host, Port = options.Port, UserName = options.User,
            Password = options.Password, VirtualHost = options.VirtualHost,
            DispatchConsumersAsync = true
        };
        var rabbit = factory.CreateConnection();
        var channel = rabbit.CreateModel();
        channel.ExchangeDeclare(options.Exchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.QueueDeclare(options.Queue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.Queue, options.Exchange, options.RoutingKey);
        channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<AlertEvaluationEvent>(args.Body.Span)
                    ?? throw new InvalidOperationException("Empty alert evaluation event.");
                await EvaluateAsync(message, stoppingToken);
                channel.BasicAck(args.DeliveryTag, false);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Alert evaluation failed; message will be retried.");
                channel.BasicNack(args.DeliveryTag, false, requeue: true);
            }
        };
        channel.BasicConsume(options.Queue, autoAck: false, consumer);
        return Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ContinueWith(_ =>
        {
            channel.Dispose();
            rabbit.Dispose();
        }, CancellationToken.None);
    }

    private async Task EvaluateAsync(AlertEvaluationEvent message, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);

        var inserted = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT dbo.AlertEvaluationInbox(EventId, ProcessedAtUtc)
            SELECT @EventId, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertEvaluationInbox WITH (UPDLOCK, HOLDLOCK) WHERE EventId=@EventId);
            """, new { message.EventId }, transaction, cancellationToken: cancellationToken));
        if (inserted == 0) { transaction.Commit(); return; }

        const string sql = """
            DECLARE @Now DATETIMEOFFSET = @OccurredAtUtc;
            DECLARE @Fired TABLE (RuleId UNIQUEIDENTIFIER PRIMARY KEY);

            INSERT dbo.Alerts(Id,TenantId,RuleId,RuleName,Provider,Status,MatchCount,WindowMinutes,CreatedAt,IsRead)
            OUTPUT INSERTED.RuleId INTO @Fired
            SELECT NEWID(), r.TenantId, r.Id, r.Name, r.Provider, r.Status,
                   COALESCE((SELECT SUM(c.MessageCount) FROM dbo.AlertStatusCounters c
                             WHERE c.TenantId=r.TenantId AND c.Status=r.Status
                               AND c.BucketStartUtc>=DATEADD(MINUTE,-r.WindowMinutes,@Now)
                               AND (r.Provider IS NULL OR c.Provider=r.Provider)),0),
                   r.WindowMinutes, @Now, 0
            FROM dbo.AlertRules r WITH (UPDLOCK, HOLDLOCK)
            WHERE r.TenantId=@TenantId AND r.IsActive=1 AND r.Status=@Status
              AND (r.Provider IS NULL OR r.Provider=@Provider)
              AND (r.IsTriggered=0 OR (r.RepeatMode=2 AND
                   (r.LastTriggeredAt IS NULL OR r.LastTriggeredAt<=DATEADD(MINUTE,-r.RepeatIntervalMinutes,@Now))))
              AND COALESCE((SELECT SUM(c.MessageCount) FROM dbo.AlertStatusCounters c
                            WHERE c.TenantId=r.TenantId AND c.Status=r.Status
                              AND c.BucketStartUtc>=DATEADD(MINUTE,-r.WindowMinutes,@Now)
                              AND (r.Provider IS NULL OR c.Provider=r.Provider)),0) >= r.Threshold;

            UPDATE r SET IsTriggered=1, LastTriggeredAt=@Now, UpdatedAt=@Now
            FROM dbo.AlertRules r INNER JOIN @Fired f ON f.RuleId=r.Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = message.TenantId, Provider = message.Provider, Status = message.Status,
            OccurredAtUtc = message.OccurredAtUtc
        }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
    }
}
