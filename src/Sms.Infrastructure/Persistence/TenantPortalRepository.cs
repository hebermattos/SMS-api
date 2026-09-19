using Dapper;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantPortalRepository(
    SqlConnectionFactory factory,
    ReportingSqlConnectionFactory reportingFactory) : ITenantPortalRepository
{
    public async Task<TenantOverview?> GetOverviewAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var connection = factory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantPortalRepository.GetOverviewAsync.01.sql"),
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var name = await results.ReadSingleOrDefaultAsync<string>();
        var providers = (await results.ReadAsync<AvailableProvider>()).AsList();
        if (name is null) return null;

        using var reportingConnection = reportingFactory.CreateConnection();
        var counts = await reportingConnection.QuerySingleOrDefaultAsync<Counts>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantPortalRepository.GetOverviewCountsAsync.01.sql"),
            new { TenantId = tenantId }, cancellationToken: cancellationToken)) ?? new Counts(0, 0, 0, 0, 0);
        return new(name, counts.Outbound, counts.Inbound, counts.Delivered, counts.Failed, counts.Pending, providers);
    }
    private sealed record Counts(long Outbound, long Inbound, long Delivered, long Failed, long Pending);
}
