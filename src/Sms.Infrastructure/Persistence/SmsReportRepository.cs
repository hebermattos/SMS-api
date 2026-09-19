using Dapper;
using Sms.Application.Reports;

namespace Sms.Infrastructure.Persistence;

public sealed class SmsReportRepository(SqlConnectionFactory connectionFactory) : ISmsReportRepository
{
    public async Task<SmsReportSummary> GetTenantSummaryAsync(Guid tenantId, SmsReportFilter filter, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/SmsReportRepository.GetTenantSummaryAsync.01.sql");
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, filter.From, filter.To, filter.Status, filter.Direction, filter.Provider },
            cancellationToken: cancellationToken));
        var totals = await grid.ReadSingleAsync<SmsReportSummaryRow>();
        var providerRows = (await grid.ReadAsync<SmsReportProviderRow>()).AsList();
        var providers = providerRows.Select(row => new SmsReportProviderSummary(row.Provider, row.TotalMessages, row.Delivered, row.Failed)).ToArray();
        return new(totals.TotalMessages, totals.Scheduled, totals.Queued, totals.Sent, totals.Delivered, totals.Failed,
            totals.Received, totals.Outbound, totals.Inbound, providers);
    }

    public async Task<PlatformSmsReportSummary> GetPlatformSummaryAsync(SmsReportFilter filter, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/SmsReportRepository.GetPlatformSummaryAsync.02.sql");
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql,
            new { filter.From, filter.To, filter.Status, filter.Direction, filter.Provider },
            cancellationToken: cancellationToken));
        var totals = await grid.ReadSingleAsync<PlatformSmsReportSummaryRow>();
        var tenantRows = (await grid.ReadAsync<PlatformSmsReportTenantRow>()).AsList();
        var tenants = tenantRows.Select(row => new PlatformSmsReportTenantSummary(row.TenantId, row.TenantName,
            row.TotalMessages, row.Scheduled, row.Queued, row.Sent, row.Delivered, row.Failed, row.Received)).ToArray();
        return new(totals.TotalMessages, totals.Scheduled, totals.Queued, totals.Sent, totals.Delivered,
            totals.Failed, totals.Received, tenants);
    }

    private sealed record SmsReportSummaryRow(long TotalMessages, int Scheduled, int Queued, int Sent, int Delivered, int Failed, int Received, int Outbound, int Inbound);
    private sealed record SmsReportProviderRow(string Provider, long TotalMessages, int Delivered, int Failed);
    private sealed record PlatformSmsReportSummaryRow(long TotalMessages, int Scheduled, int Queued, int Sent, int Delivered, int Failed, int Received);
    private sealed record PlatformSmsReportTenantRow(Guid TenantId, string TenantName, long TotalMessages, int Scheduled, int Queued, int Sent, int Delivered, int Failed, int Received);
}
