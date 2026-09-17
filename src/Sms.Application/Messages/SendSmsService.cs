using Sms.Application.Common;
using Sms.Domain.Messages;

namespace Sms.Application.Messages;

public sealed class SendSmsService(
    ITenantContext tenantContext,
    ISmsMessageRepository repository,
    ISmsProviderResolver providerResolver)
{
    public async Task<SendSmsResult> SendAsync(SendSmsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.To)) throw new ArgumentException("Destination phone number is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw new ArgumentException("Message body is required.");

        var provider = providerResolver.Resolve(request.Provider);
        var from = request.From ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        var message = new SmsMessage
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId, From = from, To = request.To.Trim(), Body = request.Body,
            Provider = provider.Name, Direction = SmsDirection.Outbound, Status = SmsStatus.Queued, CreatedAt = now
        };

        await repository.InsertAsync(message, cancellationToken);

        try
        {
            var result = await provider.SendAsync(from, message.To, message.Body, cancellationToken);
            message.ProviderMessageId = result.ProviderMessageId;
            message.Status = ParseStatus(result.Status);
            message.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.UpdateStatusAsync(message.TenantId, message.Id, message.Status, message.ProviderMessageId, message.UpdatedAt.Value, cancellationToken);
            return new SendSmsResult(message.Id, provider.Name, message.ProviderMessageId, message.Status.ToString());
        }
        catch
        {
            await repository.UpdateStatusAsync(message.TenantId, message.Id, SmsStatus.Failed, null, DateTimeOffset.UtcNow, cancellationToken);
            throw;
        }
    }

    private static SmsStatus ParseStatus(string status) => status.ToLowerInvariant() switch
    {
        "sent" => SmsStatus.Sent,
        "delivered" => SmsStatus.Delivered,
        "failed" => SmsStatus.Failed,
        _ => SmsStatus.Queued
    };
}
