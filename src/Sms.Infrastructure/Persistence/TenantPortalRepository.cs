using Dapper;
using Sms.Application.Administration;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantPortalRepository(SqlConnectionFactory factory) : ITenantPortalRepository
{
    public async Task<TenantOverview?> GetOverviewAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT Name FROM dbo.Tenants WHERE Id=@TenantId AND IsActive=1;
            SELECT
                COUNT_BIG(CASE WHEN Direction=@Outbound THEN 1 END) AS Outbound,
                COUNT_BIG(CASE WHEN Direction=@Inbound THEN 1 END) AS Inbound,
                COUNT_BIG(CASE WHEN Direction=@Outbound AND Status=@Delivered THEN 1 END) AS Delivered,
                COUNT_BIG(CASE WHEN Direction=@Outbound AND Status=@Failed THEN 1 END) AS Failed,
                COUNT_BIG(CASE WHEN Direction=@Outbound AND Status IN (@Queued, @Sent) THEN 1 END) AS Pending
            FROM dbo.SmsMessages WHERE TenantId=@TenantId;
            SELECT Provider AS Name, FromNumber, IsDefault FROM dbo.TenantSmsProviders
            WHERE TenantId=@TenantId AND IsActive=1 ORDER BY IsDefault DESC, Provider;
            """, new { TenantId = tenantId, Outbound = SmsDirection.Outbound, Inbound = SmsDirection.Inbound,
                Delivered = SmsStatus.Delivered, Failed = SmsStatus.Failed, Queued = SmsStatus.Queued, Sent = SmsStatus.Sent }, cancellationToken: cancellationToken));
        var name = await results.ReadSingleOrDefaultAsync<string>();
        var counts = await results.ReadSingleAsync<Counts>();
        var providers = (await results.ReadAsync<AvailableProvider>()).AsList();
        return name is null ? null : new(name, counts.Outbound, counts.Inbound, counts.Delivered, counts.Failed, counts.Pending, providers);
    }
    private sealed record Counts(long Outbound, long Inbound, long Delivered, long Failed, long Pending);
}
