using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sms.Infrastructure.Messaging;

public sealed class TenantSmsOverviewOutboxPublisher(
    ITenantSmsOverviewOutbox outbox,
    ITenantSmsOverviewEventPublisher eventPublisher,
    ILogger<TenantSmsOverviewOutboxPublisher> logger) : BackgroundService
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
                logger.LogError(exception, "Failed to publish tenant SMS overview outbox batch.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    public async Task<int> PublishBatchAsync(CancellationToken cancellationToken = default)
    {
        var published = 0;
        foreach (var item in await outbox.GetPendingAsync(cancellationToken))
        {
            await eventPublisher.PublishAsync(item, cancellationToken);
            await outbox.MarkPublishedAsync(item.EventId, cancellationToken);
            published++;
        }

        return published;
    }
}
