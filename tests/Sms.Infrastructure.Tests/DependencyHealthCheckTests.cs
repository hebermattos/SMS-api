using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sms.Api.Health;

namespace Sms.Infrastructure.Tests;

public sealed class DependencyHealthCheckTests
{
    [Fact]
    public async Task RedisHealthCheck_ReturnsHealthyWhenRedisResponds()
    {
        var check = new DependencyHealthChecks.RedisHealthCheck(new FakeDistributedCache());

        var result = await check.CheckHealthAsync(Context(check, HealthStatus.Degraded));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task RedisHealthCheck_ReturnsConfiguredFailureStatusWhenRedisFails()
    {
        var check = new DependencyHealthChecks.RedisHealthCheck(new ThrowingDistributedCache());

        var result = await check.CheckHealthAsync(Context(check, HealthStatus.Degraded));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Redis connection failed.", result.Description);
    }

    [Fact]
    public async Task TwilioHealthCheck_TreatsNonServerHttpResponseAsReachable()
    {
        var check = new DependencyHealthChecks.TwilioHealthCheck(
            new Factory(new HttpClient(new Handler(HttpStatusCode.Unauthorized))
            {
                BaseAddress = new Uri("https://example.test/")
            }));

        var result = await check.CheckHealthAsync(Context(check, HealthStatus.Degraded));

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("HTTP 401", result.Description);
    }

    [Fact]
    public async Task TwilioHealthCheck_ReturnsDegradedForServerFailure()
    {
        var check = new DependencyHealthChecks.TwilioHealthCheck(
            new Factory(new HttpClient(new Handler(HttpStatusCode.ServiceUnavailable))
            {
                BaseAddress = new Uri("https://example.test/")
            }));

        var result = await check.CheckHealthAsync(Context(check, HealthStatus.Degraded));

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("HTTP 503", result.Description);
    }

    [Fact]
    public async Task RabbitMqHealthCheck_ReturnsHealthyWhenTcpEndpointAcceptsConnections()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "127.0.0.1",
            ["RabbitMq:Port"] = port.ToString()
        }).Build();
        var check = new DependencyHealthChecks.RabbitMqHealthCheck(configuration);

        var result = await check.CheckHealthAsync(Context(check, HealthStatus.Unhealthy));

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static HealthCheckContext Context(IHealthCheck check, HealthStatus failureStatus) => new()
    {
        Registration = new HealthCheckRegistration(
            "test",
            _ => check,
            failureStatus,
            Array.Empty<string>())
    };

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class Handler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private class FakeDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) { }
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
    }

    private sealed class ThrowingDistributedCache : FakeDistributedCache
    {
        public new Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Redis unavailable.");
    }
}
