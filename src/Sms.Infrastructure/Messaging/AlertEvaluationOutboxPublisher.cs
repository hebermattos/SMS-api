using Dapper;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertEvaluationOutboxPublisher(
    SqlConnectionFactory connectionFactory,
    IBus bus,
    ILogger<AlertEvaluationOutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan ActiveDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxIdleDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = ActiveDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishBatchAsync(stoppingToken);
                delay = published > 0
                    ? ActiveDelay
                    : TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, MaxIdleDelay.TotalSeconds));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to publish alert evaluation outbox.");
                delay = MaxIdleDelay;
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<int> PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<OutboxRow>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/AlertEvaluationOutboxPublisher.PublishBatchAsync.01.sql"), cancellationToken: cancellationToken));

        var published = 0;
        foreach (var row in rows)
        {
            await bus.Publish(new AlertEvaluationEvent(
                row.EventId, row.TenantId, row.Provider, row.Status, row.OccurredAtUtc), cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/AlertEvaluationOutboxPublisher.PublishBatchAsync.02.sql"),
                new { Id = row.EventId }, cancellationToken: cancellationToken));
            published++;
        }

        return published;
    }

    private sealed record OutboxRow(Guid EventId, Guid TenantId, string Provider, Sms.Domain.Messages.SmsStatus Status, DateTimeOffset OccurredAtUtc);
}
