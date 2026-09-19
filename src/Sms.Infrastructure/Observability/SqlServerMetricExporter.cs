using Dapper;
using Microsoft.Data.SqlClient;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Sms.Infrastructure.Observability;

public sealed class SqlServerMetricExporter(string connectionString) : BaseExporter<Metric>
{
    private static readonly string InsertSql = Sms.Infrastructure.Sql.SqlQuery.Load("Observability/SqlServerMetricExporter.SqlServerMetricExporter.01.sql");

    public override ExportResult Export(in Batch<Metric> batch)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            foreach (var metric in batch)
            {
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    var (value, count) = GetValue(metric.MetricType, point);
                    connection.Execute(InsertSql, new
                    {
                        Timestamp = point.EndTime,
                        Name = Limit(metric.Name, 256),
                        Unit = Limit(metric.Unit, 64),
                        MetricType = metric.MetricType.ToString(),
                        Value = value,
                        Count = count,
                        Attributes = ObservabilityTags.Serialize(point.Tags)
                    }, transaction);
                }
            }
            transaction.Commit();
            return ExportResult.Success;
        }
        catch
        {
            return ExportResult.Failure;
        }
    }

    private static (double? Value, long? Count) GetValue(MetricType type, in MetricPoint point) => type switch
    {
        MetricType.LongSum or MetricType.LongSumNonMonotonic => (point.GetSumLong(), null),
        MetricType.DoubleSum or MetricType.DoubleSumNonMonotonic => (point.GetSumDouble(), null),
        MetricType.LongGauge => (point.GetGaugeLastValueLong(), null),
        MetricType.DoubleGauge => (point.GetGaugeLastValueDouble(), null),
        MetricType.Histogram or MetricType.ExponentialHistogram =>
            (point.GetHistogramSum(), point.GetHistogramCount()),
        _ => (null, null)
    };

    private static string? Limit(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
