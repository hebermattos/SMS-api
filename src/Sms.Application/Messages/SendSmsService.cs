using Sms.Application.Common;
using Sms.Application.OptOut;
using Sms.Domain.Messages;

namespace Sms.Application.Messages;

public sealed class SendSmsService(
    ITenantContext tenantContext,
    ISmsMessageRepository repository,
    ISmsProviderResolver providerResolver,
    ISmsSendEventPublisher eventPublisher,
    OptOutService optOut,
    SendSmsValidator validator,
    TimeProvider clock)
{
    public async Task<SendSmsResult> SendAsync(SendSmsRequest request, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var scheduledAtUtc = await validator.ValidateAsync(request, now, cancellationToken);

        await optOut.EnsureCanSendAsync(tenantContext.TenantId, request.To, cancellationToken);

        var provider = providerResolver.Resolve(request.Provider);
        var status = scheduledAtUtc.HasValue ? SmsStatus.Scheduled : SmsStatus.Queued;
        var message = new SmsMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            UserId = request.UserId,
            From = request.From ?? string.Empty,
            To = request.To.Trim(),
            Body = request.Body,
            Provider = provider.Name,
            Direction = SmsDirection.Outbound,
            Status = status,
            CreatedAt = now,
            ScheduledAtUtc = scheduledAtUtc
        };

        await repository.InsertAsync(message, cancellationToken);
        if (!scheduledAtUtc.HasValue)
            await eventPublisher.PublishAsync(message.TenantId, message.Id, cancellationToken);
        return new SendSmsResult(message.Id, provider.Name, null, status.ToString(), scheduledAtUtc);
    }
}
