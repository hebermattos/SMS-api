using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Sms.Infrastructure.Observability;

public sealed class SqlServerLogExporter(string connectionString) : BaseExporter<LogRecord>
{
    private const string InsertSql = """
        INSERT INTO dbo.LogEntries
            ([Timestamp], TenantId, Severity, Category, Message, TraceId, SpanId, Attributes)
        VALUES
            (@Timestamp, @TenantId, @Severity, @Category, @Message, @TraceId, @SpanId, @Attributes);
        """;

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            foreach (var record in batch)
            {
                var attributes = record.Attributes?.ToDictionary(x => x.Key, x => x.Value);
                var tenantId = TryGetTenantId(attributes);
                connection.Execute(InsertSql, new
                {
                    record.Timestamp,
                    TenantId = tenantId,
                    Severity = record.LogLevel.ToString(),
                    Category = Limit(record.CategoryName, 256),
                    Message = Limit(record.FormattedMessage ?? record.Body?.ToString() ?? string.Empty, 4000),
                    TraceId = record.TraceId == default ? null : record.TraceId.ToHexString(),
                    SpanId = record.SpanId == default ? null : record.SpanId.ToHexString(),
                    Attributes = attributes is null || attributes.Count == 0 ? null : JsonSerializer.Serialize(attributes)
                }, transaction);
            }

            transaction.Commit();
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private static Guid? TryGetTenantId(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null || !attributes.TryGetValue("TenantId", out var value)) return null;
        return Guid.TryParse(value?.ToString(), out var tenantId) ? tenantId : null;
    }

    private static string Limit(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maxLength ? value : value[..maxLength];
}
