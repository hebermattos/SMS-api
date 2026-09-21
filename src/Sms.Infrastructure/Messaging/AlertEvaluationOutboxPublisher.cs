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
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);

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
                logger.LogError(exception, "Failed to publish alert evaluation outbox batch.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
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
