using System.Diagnostics;
using Dapper;
using Npgsql;
using OpenTelemetry;

namespace Sms.Infrastructure.Observability;

public sealed class PostgresTraceExporter(string connectionString) : BaseExporter<Activity>
{
    private static readonly string InsertSql = Sms.Infrastructure.Sql.SqlQuery.Load("Observability/SqlServerTraceExporter.SqlServerTraceExporter.01.sql");

    public override ExportResult Export(in Batch<Activity> batch)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            foreach (var activity in batch)
            {
                connection.Execute(InsertSql, new
                {
                    StartedAt = new DateTimeOffset(activity.StartTimeUtc),
                    DurationMilliseconds = activity.Duration.TotalMilliseconds,
                    TraceId = activity.TraceId.ToHexString(),
                    SpanId = activity.SpanId.ToHexString(),
                    ParentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString(),
                    Name = Limit(activity.DisplayName, 256),
                    Source = Limit(activity.Source.Name, 256),
                    Kind = activity.Kind.ToString(),
                    Status = activity.Status.ToString(),
                    Attributes = ObservabilityTags.Serialize(activity.TagObjects)
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

    private static string? Limit(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
