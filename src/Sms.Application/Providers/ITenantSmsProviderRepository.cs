namespace Sms.Application.Providers;

public sealed record TenantSmsProviderConfiguration(
    Guid TenantId,
    string Provider,
    string AccountId,
    string ApiSecret,
    string? FromNumber,
    bool IsDefault,
    bool IsActive);

public interface ITenantSmsProviderRepository
{
    Task<TenantSmsProviderConfiguration?> GetAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default);
    Task<TenantSmsProviderConfiguration?> GetDefaultAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantSmsProviderConfiguration?> GetByAccountAsync(string provider, string accountId, CancellationToken cancellationToken = default);
    Task UpsertAsync(TenantSmsProviderConfiguration configuration, CancellationToken cancellationToken = default);
}
