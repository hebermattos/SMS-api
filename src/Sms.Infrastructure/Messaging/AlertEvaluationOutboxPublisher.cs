using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertEvaluationOutboxPublisher(
    SqlConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<AlertEvaluationOutboxPublisher> logger) : BackgroundService
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
                logger.LogError(exception, "Failed to publish alert evaluation outbox.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<OutboxRow>(new CommandDefinition("""
            SELECT TOP (100) Id AS EventId, TenantId, Provider, Status, OccurredAtUtc
            FROM dbo.AlertEvaluationOutbox WITH (READPAST, UPDLOCK, ROWLOCK)
            WHERE PublishedAtUtc IS NULL
            ORDER BY CreatedAtUtc, Id;
            """, cancellationToken: cancellationToken));

        if (!rows.Any()) return;

        var factory = new ConnectionFactory
        {
            HostName = options.Host, Port = options.Port, UserName = options.User,
            Password = options.Password, VirtualHost = options.VirtualHost,
            DispatchConsumersAsync = true
        };
        using var rabbit = factory.CreateConnection();
        using var channel = rabbit.CreateModel();
        channel.ExchangeDeclare(options.Exchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.QueueDeclare(options.Queue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.Queue, options.Exchange, options.RoutingKey);
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        foreach (var row in rows)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(new AlertEvaluationEvent(
                row.EventId, row.TenantId, row.Provider, row.Status, row.OccurredAtUtc));
            channel.BasicPublish(options.Exchange, options.RoutingKey, properties, body);
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.AlertEvaluationOutbox SET PublishedAtUtc=SYSUTCDATETIME(), AttemptCount=AttemptCount+1, LastAttemptAtUtc=SYSUTCDATETIME() WHERE Id=@Id AND PublishedAtUtc IS NULL;",
                new { Id = row.EventId }, cancellationToken: cancellationToken));
        }
    }

    private sealed record OutboxRow(Guid EventId, Guid TenantId, string Provider, Sms.Domain.Messages.SmsStatus Status, DateTimeOffset OccurredAtUtc);
}
