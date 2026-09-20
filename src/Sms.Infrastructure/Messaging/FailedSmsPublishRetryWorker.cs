using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Application.Messages;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class FailedSmsPublishRetryWorker(
    IFailedSmsPublishSource source,
    ISmsMessageRepository repository,
    IBus bus,
    ILogger<FailedSmsPublishRetryWorker> logger) : BackgroundService
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
                logger.LogError(exception, "Failed to retry SMS queue publications.");
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
            try
            {
                await bus.Publish(
                    new SmsSendEvent(Guid.NewGuid(), row.TenantId, row.MessageId),
                    cancellationToken);
                await repository.UpdateStatusAsync(
                    row.TenantId,
                    row.MessageId,
                    SmsStatus.Queued,
                    null,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to republish SMS message {MessageId}.", row.MessageId);
            }
        }

        return published;
    }
}
