using Dapper;
using Sms.Application.Logs;

namespace Sms.Infrastructure.Persistence;

public sealed class LogEntryRepository(LogsSqlConnectionFactory connectionFactory) : ILogEntryRepository
{
    public async Task<IReadOnlyList<LogEntry>> GetAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, [Timestamp], Severity, Category, Message, TraceId, SpanId, Attributes
            FROM dbo.LogEntries
            WHERE TenantId = @TenantId
              AND (@From IS NULL OR [Timestamp] >= @From)
              AND (@To IS NULL OR [Timestamp] < @To)
            ORDER BY [Timestamp] DESC, Id DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<LogEntry>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, From = from, To = to, Skip = skip, Take = take },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
