using Sms.Domain.Messages;
using Sms.Application.OptOut;

namespace Sms.Application.Messages;

public sealed class ReceiveSmsWebhookService(ISmsMessageRepository messages, OptOutService optOut)
{
    public async Task<SmsWebhookResult> ReceiveAsync(ISmsWebhookParser parser, string authorization,
        Stream body, SmsDirection direction, CancellationToken cancellationToken = default)
    {
        // Authenticate and validate the entire batch before making any changes.
        var batch = await parser.ParseAsync(authorization, body, direction, cancellationToken);
        if (batch.Result != SmsWebhookResult.Accepted) return batch.Result;

        foreach (var item in batch.Events)
        {
            if (item.Direction == SmsDirection.Inbound)
            {
                await optOut.ProcessInboundAsync(item.TenantId, item.From, item.Body, item.Time, cancellationToken);
                await messages.InsertInboundIfNotExistsAsync(new SmsMessage
                {
                    Id = Guid.NewGuid(), TenantId = item.TenantId, Provider = item.Provider,
                    ProviderMessageId = item.ProviderMessageId, Direction = item.Direction,
                    Status = item.Status, From = item.From, To = item.To, Body = item.Body,
                    CreatedAt = item.Time
                }, cancellationToken);
            }
            else
            {
                await messages.UpdateStatusByProviderMessageIdAsync(item.TenantId, item.Provider,
                    item.ProviderMessageId, item.Status, item.Time, cancellationToken);
            }
        }

        return SmsWebhookResult.Accepted;
    }
}
