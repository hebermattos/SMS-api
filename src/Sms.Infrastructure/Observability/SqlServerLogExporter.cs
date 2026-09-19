using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Sms.Infrastructure.Observability;

public sealed class SqlServerLogExporter(string connectionString) : BaseExporter<LogRecord>
{
    private static readonly HashSet<string> ActivityCategories =
    [
        "Sms.Api.Middleware.ClientLoginAuditMiddleware",
        "Sms.Api.Middleware.PlatformAuditMiddleware",
        "Sms.Api.Middleware.RequestAuditMiddleware"
    ];

    private const string InsertActivitySql = """
        INSERT INTO dbo.UserActivityLogs
            ([Timestamp], TenantId, Severity, Category, Message, TraceId, SpanId, Attributes)
        VALUES
            (@Timestamp, @TenantId, @Severity, @Category, @Message, @TraceId, @SpanId, @Attributes);
        """;

    private const string InsertSystemSql = """
        INSERT INTO dbo.SystemLogs
            ([Timestamp], Severity, Category, Message, TraceId, SpanId, Attributes)
        VALUES
            (@Timestamp, @Severity, @Category, @Message, @TraceId, @SpanId, @Attributes);
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
                var values = new
                {
                    record.Timestamp,
                    TenantId = TryGetTenantId(attributes),
                    Severity = record.LogLevel.ToString(),
                    Category = Limit(record.CategoryName, 256),
                    Message = Limit(GetStoredMessage(record, attributes), 4000),
                    TraceId = record.TraceId == default ? null : record.TraceId.ToHexString(),
                    SpanId = record.SpanId == default ? null : record.SpanId.ToHexString(),
                    Attributes = LogAttributeSanitizer.Serialize(attributes)
                };

                if (IsActivity(record.CategoryName))
                    connection.Execute(InsertActivitySql, values, transaction);

                if (record.LogLevel is LogLevel.Error or LogLevel.Critical)
                    connection.Execute(InsertSystemSql, values, transaction);
            }

            transaction.Commit();
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    internal static bool IsActivity(string? category) =>
        category is not null && ActivityCategories.Contains(category);

    private static string GetStoredMessage(
        LogRecord record,
        IReadOnlyDictionary<string, object?>? attributes)
    {
        if (IsActivity(record.CategoryName))
        {
            if (attributes is not null
                && attributes.TryGetValue("Activity", out var activity)
                && activity is string description)
                return description;

            return record.FormattedMessage ?? record.Body?.ToString() ?? string.Empty;
        }

        if (attributes is not null
            && attributes.TryGetValue("{OriginalFormat}", out var template)
            && template is string messageTemplate)
            return messageTemplate;

        return record.Body?.ToString() ?? string.Empty;
    }

    private static Guid? TryGetTenantId(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null || !attributes.TryGetValue("TenantId", out var value)) return null;
        return Guid.TryParse(value?.ToString(), out var tenantId) ? tenantId : null;
    }

    private static string Limit(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maxLength ? value : value[..maxLength];
}
