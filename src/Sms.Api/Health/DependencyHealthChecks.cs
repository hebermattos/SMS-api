using System.Net;
using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sms.Infrastructure.Messaging;

namespace Sms.Api.Health;

public static class DependencyHealthChecks
{
    private const string TwilioClientName = "Health.Twilio";
    private const string BandwidthClientName = "Health.Bandwidth";

    public static IServiceCollection AddDependencyHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(TwilioClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.twilio.com/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddHttpClient(BandwidthClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.bandwidth.com/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.AddHealthChecks()
            .AddCheck<ApplicationDatabaseHealthCheck>(
                "sql.application",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "internal"],
                timeout: TimeSpan.FromSeconds(3))
            .AddCheck<ObservabilityDatabaseHealthCheck>(
                "sql.observability",
                failureStatus: HealthStatus.Degraded,
                tags: ["database", "internal"],
                timeout: TimeSpan.FromSeconds(3))
            .AddCheck<ReportingDatabaseHealthCheck>(
                "sql.reporting",
                failureStatus: HealthStatus.Degraded,
                tags: ["database", "internal"],
                timeout: TimeSpan.FromSeconds(3))
            .AddCheck<RedisHealthCheck>(
                "redis",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "internal"],
                timeout: TimeSpan.FromSeconds(3))
            .AddCheck<RabbitMqHealthCheck>(
                "rabbitmq",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["messaging", "internal"],
                timeout: TimeSpan.FromSeconds(3))
            .AddCheck<TwilioHealthCheck>(
                "twilio",
                failureStatus: HealthStatus.Degraded,
                tags: ["provider", "external"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<BandwidthHealthCheck>(
                "bandwidth",
                failureStatus: HealthStatus.Degraded,
                tags: ["provider", "external"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    private abstract class SqlServerHealthCheck(string connectionString) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                command.CommandTimeout = 2;
                var result = await command.ExecuteScalarAsync(cancellationToken);

                return Convert.ToInt32(result) == 1
                    ? HealthCheckResult.Healthy("SQL Server responded.")
                    : Failure(context, "SQL Server returned an unexpected result.");
            }
            catch
            {
                return Failure(context, "SQL Server connection failed.");
            }
        }
    }

    private sealed class ApplicationDatabaseHealthCheck(IConfiguration configuration)
        : SqlServerHealthCheck(RequiredConnectionString(configuration, "SqlServer"));

    private sealed class ObservabilityDatabaseHealthCheck(IConfiguration configuration)
        : SqlServerHealthCheck(RequiredConnectionString(configuration, "LogsSqlServer"));

    private sealed class ReportingDatabaseHealthCheck(IConfiguration configuration)
        : SqlServerHealthCheck(RequiredConnectionString(configuration, "ReportingSqlServer"));

    private sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await cache.GetAsync("health:redis-probe", cancellationToken);
                return HealthCheckResult.Healthy("Redis responded.");
            }
            catch
            {
                return Failure(context, "Redis connection failed.");
            }
        }
    }

    private sealed class RabbitMqHealthCheck : IHealthCheck
    {
        private readonly string _host;
        private readonly int _port;

        public RabbitMqHealthCheck(IConfiguration configuration)
        {
            var options = RabbitMqAlertOptions.From(configuration);
            _host = options.Host;
            _port = options.Port;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_host, _port, cancellationToken);
                return client.Connected
                    ? HealthCheckResult.Healthy("RabbitMQ TCP endpoint is reachable.")
                    : Failure(context, "RabbitMQ connection failed.");
            }
            catch
            {
                return Failure(context, "RabbitMQ connection failed.");
            }
        }
    }

    private abstract class ExternalHttpHealthCheck(
        IHttpClientFactory httpClientFactory,
        string clientName,
        string providerName) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = httpClientFactory.CreateClient(clientName);
                using var request = new HttpRequestMessage(HttpMethod.Get, "");
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if ((int)response.StatusCode >= 500)
                    return Failure(context, $"{providerName} returned HTTP {(int)response.StatusCode}.");

                return HealthCheckResult.Healthy(
                    $"{providerName} API is reachable (HTTP {(int)response.StatusCode}).");
            }
            catch
            {
                return Failure(context, $"{providerName} API is unreachable.");
            }
        }
    }

    private sealed class TwilioHealthCheck(IHttpClientFactory httpClientFactory)
        : ExternalHttpHealthCheck(httpClientFactory, TwilioClientName, "Twilio");

    private sealed class BandwidthHealthCheck(IHttpClientFactory httpClientFactory)
        : ExternalHttpHealthCheck(httpClientFactory, BandwidthClientName, "Bandwidth");

    private static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' is not configured.");

    private static HealthCheckResult Failure(HealthCheckContext context, string description) =>
        new(context.Registration.FailureStatus, description);
}
