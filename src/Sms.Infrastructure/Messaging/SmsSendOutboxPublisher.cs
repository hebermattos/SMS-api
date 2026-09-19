using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class SmsSendOutboxPublisher(
    SqlConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<SmsSendOutboxPublisher> logger) : BackgroundService
{
    private readonly RabbitMqAlertOptions options = RabbitMqAlertOptions.From(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to publish SMS send outbox.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SmsSendEvent>(new CommandDefinition("""
            SELECT TOP (100) Id AS EventId, TenantId, MessageId
            FROM dbo.SmsSendOutbox WITH (READPAST, UPDLOCK, ROWLOCK)
            WHERE PublishedAtUtc IS NULL
            ORDER BY CreatedAtUtc, Id;
            """, cancellationToken: cancellationToken));

        if (!rows.Any()) return;

        var factory = new ConnectionFactory
        {
            HostName = options.Host, Port = options.Port, UserName = options.User,
            Password = options.Password, VirtualHost = options.VirtualHost
        };
        using var rabbit = factory.CreateConnection();
        using var channel = rabbit.CreateModel();
        channel.ExchangeDeclare(options.SendExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.QueueDeclare(options.SendQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.SendQueue, options.SendExchange, options.SendRoutingKey);
        channel.ConfirmSelect();

        foreach (var row in rows)
        {
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = row.EventId.ToString();
            channel.BasicPublish(options.SendExchange, options.SendRoutingKey, properties, JsonSerializer.SerializeToUtf8Bytes(row));
            channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE dbo.SmsSendOutbox
                SET PublishedAtUtc=SYSUTCDATETIME(), AttemptCount=AttemptCount+1, LastAttemptAtUtc=SYSUTCDATETIME()
                WHERE Id=@EventId AND PublishedAtUtc IS NULL;
                """, row, cancellationToken: cancellationToken));
        }
    }
}
