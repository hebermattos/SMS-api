using Dapper;
using Sms.Application.Reports;

namespace Sms.Infrastructure.Persistence;

public sealed class SmsReportRepository(ReportingSqlConnectionFactory connectionFactory) : ISmsReportRepository
{
    public async Task<SmsReportSummary> GetTenantSummaryAsync(Guid tenantId, SmsReportFilter filter, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/SmsReportRepository.GetTenantSummaryAsync.01.sql");
        using var connection = connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<SmsReportProviderRow>(new CommandDefinition(sql,
            new { TenantId = tenantId, filter.From, filter.To, filter.Status, filter.Direction, filter.Provider },
            cancellationToken: cancellationToken))).AsList();

        var providers = rows.Select(row => new SmsReportProviderSummary(row.Provider, row.TotalMessages, row.Delivered, row.Failed)).ToArray();
        return new(rows.Sum(x => x.TotalMessages), rows.Sum(x => x.Scheduled), rows.Sum(x => x.Queued), rows.Sum(x => x.Sent),
            rows.Sum(x => x.Delivered), rows.Sum(x => x.Failed), rows.Sum(x => x.Received), rows.Sum(x => x.Outbound),
            rows.Sum(x => x.Inbound), providers);
    }

    public async Task<IReadOnlyList<UserSmsReportSummary>> GetUserSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/SmsReportRepository.GetUserSummaryAsync.03.sql");
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<UserSmsReportSummary>(new CommandDefinition(sql, new { TenantId = tenantId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<PlatformSmsReportSummary> GetPlatformSummaryAsync(SmsReportFilter filter, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/SmsReportRepository.GetPlatformSummaryAsync.02.sql");
        using var connection = connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<PlatformSmsReportTenantRow>(new CommandDefinition(sql,
            new { filter.From, filter.To, filter.Status, filter.Direction, filter.Provider }, cancellationToken: cancellationToken))).AsList();

        var tenants = rows.Select(row => new PlatformSmsReportTenantSummary(row.TenantId, row.TenantName,
            row.TotalMessages, row.Scheduled, row.Queued, row.Sent, row.Delivered, row.Failed, row.Received)).ToArray();
        return new(rows.Sum(x => x.TotalMessages), rows.Sum(x => x.Scheduled), rows.Sum(x => x.Queued), rows.Sum(x => x.Sent),
            rows.Sum(x => x.Delivered), rows.Sum(x => x.Failed), rows.Sum(x => x.Received), tenants);
    }

    private sealed record SmsReportProviderRow(string Provider, long TotalMessages, int Scheduled, int Queued, int Sent,
        int Delivered, int Failed, int Received, int Outbound, int Inbound);
    private sealed record PlatformSmsReportTenantRow(Guid TenantId, string TenantName, long TotalMessages, int Scheduled,
        int Queued, int Sent, int Delivered, int Failed, int Received);
}
