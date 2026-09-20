using MassTransit;
using Microsoft.Extensions.Logging;
using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Application.OptOut;
using Sms.Domain.Messages;

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
            var sendResult = await provider.SendAsync(message.From, message.To, message.Body, cancellationToken);
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
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SMS provider rejected queued message {MessageId}.", sendEvent.MessageId);

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
