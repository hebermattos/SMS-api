using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sms.Infrastructure.Messaging;

public sealed class TenantSmsOverviewOutboxPublisher(
    ITenantSmsOverviewOutbox outbox,
    ITenantSmsOverviewEventPublisher eventPublisher,
    ILogger<TenantSmsOverviewOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Failed to publish tenant SMS overview events."); }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    public async Task PublishBatchAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in await outbox.GetPendingAsync(cancellationToken))
        {
            await eventPublisher.PublishAsync(item, cancellationToken);
            await outbox.MarkPublishedAsync(item.EventId, cancellationToken);
        }
    }
}
