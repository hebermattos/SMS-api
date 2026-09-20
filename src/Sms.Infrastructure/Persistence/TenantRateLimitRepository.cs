using Dapper;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantRateLimitRepository(SqlConnectionFactory connections) : ITenantRateLimitRepository
{
    public async Task<TenantRateLimitSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantRateLimitRepository.GetAsync.01.sql");
        using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantRateLimitSettings>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))
            ?? new TenantRateLimitSettings(600, 60);
    }

    public async Task SaveAsync(Guid tenantId, TenantRateLimitSettings settings, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantRateLimitRepository.SaveAsync.01.sql");
        using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            settings.RequestsPerMinute,
            settings.SmsPerMinute
        }, cancellationToken: cancellationToken));
    }
}
