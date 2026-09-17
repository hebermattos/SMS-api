using Dapper;
using Sms.Application.Providers;
using Sms.Application.Security;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantSmsProviderRepository(SqlConnectionFactory connectionFactory, ISecretProtector secretProtector) : ITenantSmsProviderRepository
{
    public async Task<TenantSmsProviderConfiguration?> GetAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive
            FROM dbo.TenantSmsProviders
            WHERE TenantId = @TenantId AND Provider = @Provider AND IsActive = 1;
            """;
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(
            new CommandDefinition(sql, new { TenantId = tenantId, Provider = provider }, cancellationToken: cancellationToken));
        return Decrypt(row);
    }

    public async Task<TenantSmsProviderConfiguration?> GetDefaultAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive
            FROM dbo.TenantSmsProviders
            WHERE TenantId = @TenantId AND IsDefault = 1 AND IsActive = 1;
            """;
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TenantSmsProviderConfiguration>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return Decrypt(row);
    }

    public async Task UpsertAsync(TenantSmsProviderConfiguration configuration, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.TenantSmsProviders
            SET AccountId = @AccountId, ApiSecret = @ApiSecret, FromNumber = @FromNumber,
                IsDefault = @IsDefault, IsActive = @IsActive, UpdatedAt = @Now
            WHERE TenantId = @TenantId AND Provider = @Provider;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.TenantSmsProviders
                    (Id, TenantId, Provider, AccountId, ApiSecret, FromNumber, IsDefault, IsActive, CreatedAt)
                VALUES
                    (@Id, @TenantId, @Provider, @AccountId, @ApiSecret, @FromNumber, @IsDefault, @IsActive, @Now);
            END
            """;

        var encryptedSecret = secretProtector.Protect(configuration.ApiSecret);
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(), configuration.TenantId, configuration.Provider, configuration.AccountId,
            ApiSecret = encryptedSecret, configuration.FromNumber, configuration.IsDefault, configuration.IsActive,
            Now = DateTimeOffset.UtcNow
        }, cancellationToken: cancellationToken));
    }

    private TenantSmsProviderConfiguration? Decrypt(TenantSmsProviderConfiguration? configuration) =>
        configuration is null ? null : configuration with { ApiSecret = secretProtector.Unprotect(configuration.ApiSecret) };
}
