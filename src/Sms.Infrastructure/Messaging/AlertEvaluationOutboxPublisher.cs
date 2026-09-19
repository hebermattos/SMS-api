using Dapper;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertEvaluationOutboxPublisher(
    SqlConnectionFactory connectionFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<AlertEvaluationOutboxPublisher> logger) : BackgroundService
{
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

        foreach (var row in rows)
        {
            await publishEndpoint.Publish(new AlertEvaluationEvent(
                row.EventId, row.TenantId, row.Provider, row.Status, row.OccurredAtUtc), cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE dbo.AlertEvaluationOutbox SET PublishedAtUtc=SYSUTCDATETIME(), AttemptCount=AttemptCount+1, LastAttemptAtUtc=SYSUTCDATETIME() WHERE Id=@Id AND PublishedAtUtc IS NULL;",
                new { Id = row.EventId }, cancellationToken: cancellationToken));
        }
    }

    private sealed record OutboxRow(Guid EventId, Guid TenantId, string Provider, Sms.Domain.Messages.SmsStatus Status, DateTimeOffset OccurredAtUtc);
}
