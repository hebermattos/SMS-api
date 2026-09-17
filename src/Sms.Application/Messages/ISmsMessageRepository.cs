using Sms.Domain.Messages;

namespace Sms.Application.Messages;

public interface ISmsMessageRepository
{
    Task<SmsMessage?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SmsMessage>> GetHistoryAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default);
    Task InsertAsync(SmsMessage message, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid tenantId, Guid id, SmsStatus status, string? providerMessageId, DateTimeOffset updatedAt, CancellationToken cancellationToken = default);
    Task UpdateStatusByProviderMessageIdAsync(Guid tenantId, string provider, string providerMessageId, SmsStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default);
}
