using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sms.Infrastructure.Messaging;

public sealed class SmsQueuePublisherWorker(
    IFailedSmsPublishSource source,
    IBus bus,
    ILogger<SmsQueuePublisherWorker> logger) : BackgroundService
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
                logger.LogError(exception, "Failed to publish SMS messages to the queue.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    internal async Task<int> PublishBatchAsync(CancellationToken cancellationToken = default)
    {
        var rows = await source.GetPendingAsync(cancellationToken);

        var published = 0;
        foreach (var row in rows)
        {
            if (!await source.TryMarkQueuedAsync(row.TenantId, row.MessageId, cancellationToken))
                continue;

            try
            {
                await bus.Publish(
                    new SmsSendEvent(Guid.NewGuid(), row.TenantId, row.MessageId),
                    cancellationToken);
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await source.MarkNotQueuedAsync(row.TenantId, row.MessageId, CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                await source.MarkNotQueuedAsync(row.TenantId, row.MessageId, cancellationToken);
                logger.LogError(exception, "Failed to publish SMS message {MessageId}.", row.MessageId);
            }
        }

        return published;
    }
}
