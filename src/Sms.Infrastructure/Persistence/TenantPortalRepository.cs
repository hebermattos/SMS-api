using Dapper;
using Sms.Application.Administration;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantPortalRepository(SqlConnectionFactory factory) : ITenantPortalRepository
{
    public async Task<TenantOverview?> GetOverviewAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantPortalRepository.GetOverviewAsync.01.sql"), new { TenantId = tenantId, Outbound = SmsDirection.Outbound, Inbound = SmsDirection.Inbound,
                Delivered = SmsStatus.Delivered, Failed = SmsStatus.Failed, Queued = SmsStatus.Queued, Sent = SmsStatus.Sent }, cancellationToken: cancellationToken));
        var name = await results.ReadSingleOrDefaultAsync<string>();
        var counts = await results.ReadSingleAsync<Counts>();
        var providers = (await results.ReadAsync<AvailableProvider>()).AsList();
        return name is null ? null : new(name, counts.Outbound, counts.Inbound, counts.Delivered, counts.Failed, counts.Pending, providers);
    }
    private sealed record Counts(long Outbound, long Inbound, long Delivered, long Failed, long Pending);
}
