using MassTransit;
using Microsoft.Extensions.Logging;
using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Application.OptOut;
using Sms.Domain.Messages;
using Sms.Infrastructure.Observability;

namespace Sms.Infrastructure.Messaging;

public sealed class SmsSendConsumer(
    IWorkerTenantContext tenantContext,
    ISmsMessageRepository repository,
    ISmsProviderResolver providerResolver,
    OptOutService optOut,
    ILogger<SmsSendConsumer> logger) : IConsumer<SmsSendEvent>
{
    public async Task Consume(ConsumeContext<SmsSendEvent> context)
    {
        var sendEvent = context.Message;
        using var activity = TextRelayTelemetry.ActivitySource.StartActivity("sms.queue.consume");
        activity?.SetTag("tenant.id", sendEvent.TenantId);
        activity?.SetTag("message.id", sendEvent.MessageId);
        tenantContext.SetTenant(sendEvent.TenantId);

        var message = await repository.GetByIdAsync(
            sendEvent.TenantId,
            sendEvent.MessageId,
            context.CancellationToken)
            ?? throw new InvalidOperationException("Queued SMS message was not found.");

        if (IsScheduledMessageEvent(sendEvent))
        {
            var queued = await repository.TryQueueScheduledAsync(
                sendEvent.TenantId,
                sendEvent.MessageId,
                DateTimeOffset.UtcNow,
                context.CancellationToken);

            if (!queued)
                return;

            message.QueueStatus = SmsQueueStatus.Queued;
        }

        if (message.QueueStatus != SmsQueueStatus.Queued)
            return;

        using var claimActivity = TextRelayTelemetry.ActivitySource.StartActivity("sms.queue.claim");
        claimActivity?.SetTag("tenant.id", sendEvent.TenantId);
        claimActivity?.SetTag("message.id", sendEvent.MessageId);
        var claimed = await repository.TryClaimQueuedAsync(
            sendEvent.TenantId,
            sendEvent.MessageId,
            DateTimeOffset.UtcNow,
            context.CancellationToken);

        if (!claimed)
            return;

        message.QueueStatus = SmsQueueStatus.Processing;
        await SendAsync(sendEvent, message, context.CancellationToken);
    }

    private async Task SendAsync(SmsSendEvent sendEvent, SmsMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await optOut.EnsureCanSendAsync(sendEvent.TenantId, message.To, cancellationToken);

            var provider = providerResolver.Resolve(message.Provider);
            using var providerActivity = TextRelayTelemetry.ActivitySource.StartActivity("sms.provider.send");
            providerActivity?.SetTag("tenant.id", sendEvent.TenantId);
            providerActivity?.SetTag("message.id", sendEvent.MessageId);
            providerActivity?.SetTag("sms.provider", provider.Name);
            var sendResult = await provider.SendAsync(message.From, message.To, message.Body, cancellationToken);
            providerActivity?.SetTag("sms.status", sendResult.Status.ToString());
            await repository.UpdateStatusAsync(
                sendEvent.TenantId,
                sendEvent.MessageId,
                sendResult.Status,
                sendResult.ProviderMessageId,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TransientSmsProviderException)
        {
            // MassTransit retries the same delivery in this consumer instance. The
            // database claim remains Processing so another delivery cannot send it.
            // SendAsync is retried directly instead of returning through Consume,
            // which would reject the already claimed Processing state.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process queued SMS message {MessageId} for tenant {TenantId} with provider {Provider}.", sendEvent.MessageId, sendEvent.TenantId, message.Provider);

            await repository.UpdateStatusAsync(
                sendEvent.TenantId,
                sendEvent.MessageId,
                SmsStatus.Failed,
                null,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
    }

    private static bool IsScheduledMessageEvent(SmsSendEvent sendEvent) =>
        sendEvent.EventId == sendEvent.MessageId;
}
