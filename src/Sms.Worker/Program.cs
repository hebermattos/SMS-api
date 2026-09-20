using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Sms.Application;
using Sms.Application.Common;
using Sms.Infrastructure;
using Sms.Infrastructure.Messaging;
using Sms.Infrastructure.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss 'UTC' ";
    options.UseUtcTimestamp = true;
});

var logsConnectionString = builder.Configuration.GetConnectionString("LogsPostgres")
    ?? throw new InvalidOperationException("Connection string 'LogsPostgres' is not configured.");

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;
    options.AddProcessor(new BatchLogRecordExportProcessor(new PostgresLogExporter(logsConnectionString)));
    options.AddOtlpExporter();
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(RabbitMqMonitoringService.MeterName)
        .AddOtlpExporter());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<WorkerTenantContext>();
builder.Services.AddScoped<IWorkerTenantContext>(services => services.GetRequiredService<WorkerTenantContext>());
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<WorkerTenantContext>());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<AlertEvaluationOutboxPublisher>();
builder.Services.AddHostedService<ScheduledSmsPublisher>();
builder.Services.AddHostedService<FailedSmsPublishRetryWorker>();

await builder.Build().RunAsync();
