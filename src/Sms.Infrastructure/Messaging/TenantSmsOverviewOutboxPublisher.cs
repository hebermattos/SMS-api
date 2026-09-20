using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sms.Infrastructure.Messaging;

public sealed class TenantSmsOverviewOutboxPublisher(
    ITenantSmsOverviewOutbox outbox,
    ITenantSmsOverviewEventPublisher eventPublisher,
    ILogger<TenantSmsOverviewOutboxPublisher> logger) : BackgroundService
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
                logger.LogError(exception, "Failed to publish tenant SMS overview events.");
                delay = MaxIdleDelay;
            }

            await Task.Delay(delay, stoppingToken);
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
