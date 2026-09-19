using Sms.Application.Common;
using Sms.Domain.Messages;

namespace Sms.Application.Messages;

public sealed class SendSmsService(
    ITenantContext tenantContext,
    ISmsMessageRepository repository,
    ISmsProviderResolver providerResolver,
    ISmsSendEventPublisher eventPublisher)
{
    public async Task<SendSmsResult> SendAsync(SendSmsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.To)) throw new ArgumentException("Destination phone number is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw new ArgumentException("Message body is required.");

        var provider = providerResolver.Resolve(request.Provider);
        var message = new SmsMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId,
            From = request.From ?? string.Empty,
            To = request.To.Trim(),
            Body = request.Body,
            Provider = provider.Name,
            Direction = SmsDirection.Outbound,
            Status = SmsStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.InsertAsync(message, cancellationToken);
        await eventPublisher.PublishAsync(message.TenantId, message.Id, cancellationToken);
        return new SendSmsResult(message.Id, provider.Name, null, SmsStatus.Queued.ToString());
    }
}
