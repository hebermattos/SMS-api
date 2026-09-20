using Dapper;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class ScheduledSmsPublisher(
    SqlConnectionFactory connectionFactory,
    IBus bus,
    ILogger<ScheduledSmsPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan ActiveDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxIdleDelay = TimeSpan.FromMinutes(1);

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
                logger.LogError(exception, "Failed to publish scheduled SMS messages.");
                delay = MaxIdleDelay;
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    internal async Task<int> PublishBatchAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ScheduledMessage>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/ScheduledSmsPublisher.PublishBatchAsync.01.sql"),
            cancellationToken: cancellationToken));

        var published = 0;
        foreach (var row in rows)
        {
            await bus.Publish(new SmsSendEvent(row.MessageId, row.TenantId, row.MessageId), cancellationToken);
            published++;
        }

        return published;
    }

    private sealed record ScheduledMessage(Guid MessageId, Guid TenantId);
}
