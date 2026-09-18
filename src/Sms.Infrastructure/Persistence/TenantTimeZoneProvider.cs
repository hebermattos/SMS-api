using Dapper;
using Sms.Application.Common;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantTimeZoneProvider(SqlConnectionFactory connectionFactory) : ITenantTimeZoneProvider
{
    public async Task<TimeZoneInfo> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT TimeZoneId FROM dbo.Tenants WHERE Id=@TenantId AND IsActive=1;",
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        if (id is null) throw new KeyNotFoundException();
        return TimeZoneInfo.FindSystemTimeZoneById(id);
    }
}
