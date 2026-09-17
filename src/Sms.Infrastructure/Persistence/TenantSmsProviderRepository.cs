using Dapper;
using Sms.Application.Providers;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantSmsProviderRepository(SqlConnectionFactory connectionFactory) : ITenantSmsProviderRepository
{
    public async Task<TenantSmsProviderConfiguration?> GetAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive
            FROM dbo.TenantSmsProviders
            WHERE TenantId = @TenantId AND Provider = @Provider AND IsActive = 1;
            """;
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(
            new CommandDefinition(sql, new { TenantId = tenantId, Provider = provider }, cancellationToken: cancellationToken));
    }

    public async Task<TenantSmsProviderConfiguration?> GetDefaultAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive
            FROM dbo.TenantSmsProviders
            WHERE TenantId = @TenantId AND IsDefault = 1 AND IsActive = 1;
            """;
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
