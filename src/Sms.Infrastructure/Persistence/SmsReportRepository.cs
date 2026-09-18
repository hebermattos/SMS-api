using Dapper;
using Sms.Application.Reports;

namespace Sms.Infrastructure.Persistence;

public sealed class SmsReportRepository(SqlConnectionFactory connectionFactory) : ISmsReportRepository
{
    public async Task<SmsReportSummary> GetTenantSummaryAsync(Guid tenantId, SmsReportFilter filter, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END), 0) AS Queued,
                COALESCE(SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END), 0) AS Sent,
                COALESCE(SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END), 0) AS Failed,
                COALESCE(SUM(CASE WHEN Status = 5 THEN 1 ELSE 0 END), 0) AS Received,
                COALESCE(SUM(CASE WHEN Direction = 1 THEN 1 ELSE 0 END), 0) AS Outbound,
                COALESCE(SUM(CASE WHEN Direction = 2 THEN 1 ELSE 0 END), 0) AS Inbound
            FROM dbo.SmsMessages
            WHERE TenantId = @TenantId
              AND (@From IS NULL OR CreatedAt >= @From)
              AND (@To IS NULL OR CreatedAt < @To)
              AND (@Status IS NULL OR Status = @Status)
              AND (@Direction IS NULL OR Direction = @Direction)
              AND (@Provider IS NULL OR Provider = @Provider);

            SELECT Provider,
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN Status = 4 THEN 1 ELSE 0 END), 0) AS Failed
            FROM dbo.SmsMessages
            WHERE TenantId = @TenantId
              AND (@From IS NULL OR CreatedAt >= @From)
              AND (@To IS NULL OR CreatedAt < @To)
              AND (@Status IS NULL OR Status = @Status)
              AND (@Direction IS NULL OR Direction = @Direction)
              AND (@Provider IS NULL OR Provider = @Provider)
            GROUP BY Provider
            ORDER BY Provider;
            """;
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, filter.From, filter.To, filter.Status, filter.Direction, filter.Provider },
            cancellationToken: cancellationToken));
        var totals = await grid.ReadSingleAsync<SmsReportSummaryRow>();
        var providers = (await grid.ReadAsync<SmsReportProviderSummary>()).AsList();
        return new(totals.TotalMessages, totals.Queued, totals.Sent, totals.Delivered, totals.Failed,
            totals.Received, totals.Outbound, totals.Inbound, providers);
    }

    public async Task<PlatformSmsReportSummary> GetPlatformSummaryAsync(SmsReportFilter filter, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN m.Status = 1 THEN 1 ELSE 0 END), 0) AS Queued,
                COALESCE(SUM(CASE WHEN m.Status = 2 THEN 1 ELSE 0 END), 0) AS Sent,
                COALESCE(SUM(CASE WHEN m.Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN m.Status = 4 THEN 1 ELSE 0 END), 0) AS Failed,
                COALESCE(SUM(CASE WHEN m.Status = 5 THEN 1 ELSE 0 END), 0) AS Received
            FROM dbo.SmsMessages m
            WHERE (@From IS NULL OR m.CreatedAt >= @From)
              AND (@To IS NULL OR m.CreatedAt < @To)
              AND (@Status IS NULL OR m.Status = @Status)
              AND (@Direction IS NULL OR m.Direction = @Direction)
              AND (@Provider IS NULL OR m.Provider = @Provider);

            SELECT m.TenantId, t.Name AS TenantName,
                COUNT_BIG(1) AS TotalMessages,
                COALESCE(SUM(CASE WHEN m.Status = 1 THEN 1 ELSE 0 END), 0) AS Queued,
                COALESCE(SUM(CASE WHEN m.Status = 2 THEN 1 ELSE 0 END), 0) AS Sent,
                COALESCE(SUM(CASE WHEN m.Status = 3 THEN 1 ELSE 0 END), 0) AS Delivered,
                COALESCE(SUM(CASE WHEN m.Status = 4 THEN 1 ELSE 0 END), 0) AS Failed,
                COALESCE(SUM(CASE WHEN m.Status = 5 THEN 1 ELSE 0 END), 0) AS Received
            FROM dbo.SmsMessages m
            INNER JOIN dbo.Tenants t ON t.Id = m.TenantId
            WHERE (@From IS NULL OR m.CreatedAt >= @From)
              AND (@To IS NULL OR m.CreatedAt < @To)
              AND (@Status IS NULL OR m.Status = @Status)
              AND (@Direction IS NULL OR m.Direction = @Direction)
              AND (@Provider IS NULL OR m.Provider = @Provider)
            GROUP BY m.TenantId, t.Name
            ORDER BY t.Name;
            """;
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql,
            new { filter.From, filter.To, filter.Status, filter.Direction, filter.Provider },
            cancellationToken: cancellationToken));
        var totals = await grid.ReadSingleAsync<PlatformSmsReportSummaryRow>();
        var tenants = (await grid.ReadAsync<PlatformSmsReportTenantSummary>()).AsList();
        return new(totals.TotalMessages, totals.Queued, totals.Sent, totals.Delivered,
            totals.Failed, totals.Received, tenants);
    }

    private sealed record SmsReportSummaryRow(long TotalMessages, long Queued, long Sent, long Delivered, long Failed, long Received, long Outbound, long Inbound);
    private sealed record PlatformSmsReportSummaryRow(long TotalMessages, long Queued, long Sent, long Delivered, long Failed, long Received);
}
