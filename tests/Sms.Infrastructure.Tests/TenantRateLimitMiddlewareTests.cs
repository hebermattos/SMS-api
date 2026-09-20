using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sms.Api.RateLimiting;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Tests;

public sealed class TenantRateLimitMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutTenantClaim_BypassesRateLimit()
    {
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(1, 1, 20));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, repository);

        Assert.Equal(1, nextCalls);
        Assert.Equal(0, repository.GetCalls);
    }

    [Fact]
    public async Task InvokeAsync_WithoutLoginClaim_BypassesRateLimit()
    {
        var tenantId = Guid.NewGuid();
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(1, 1, 1));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", tenantId.ToString())], "test"));

        await middleware.InvokeAsync(context, repository);

        Assert.Equal(1, nextCalls);
        Assert.Equal(0, repository.GetCalls);
    }

    [Fact]
    public async Task InvokeAsync_ApiRequest_UsesApiLimitAndReturns429WhenExceeded()
    {
        var tenantId = Guid.NewGuid();
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(1, 10, 20));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });

        var first = Context(tenantId, HttpMethods.Get, "/api/v1/reports");
        var second = Context(tenantId, HttpMethods.Get, "/api/v1/reports");

        await middleware.InvokeAsync(first, repository);
        await middleware.InvokeAsync(second, repository);

        Assert.Equal(1, nextCalls);
        Assert.Equal(StatusCodes.Status429TooManyRequests, second.Response.StatusCode);
        Assert.Equal("60", second.Response.Headers.RetryAfter);
    }

    [Theory]
    [InlineData("/api/v1/messages")]
    [InlineData("/api/v1/messages/send")]
    [InlineData("/api/v1/messages/bulk")]
    public async Task InvokeAsync_SmsRoutes_UseSmsLimit(string path)
    {
        var tenantId = Guid.NewGuid();
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(100, 1, 20));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });

        await middleware.InvokeAsync(Context(tenantId, HttpMethods.Post, path), repository);
        var blocked = Context(tenantId, HttpMethods.Post, path);
        await middleware.InvokeAsync(blocked, repository);

        Assert.Equal(1, nextCalls);
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/message-assistant/improve")]
    [InlineData("/api/v1/message-assistant/validate")]
    public async Task InvokeAsync_AiRoutes_UseConfiguredOllamaLimit(string path)
    {
        var tenantId = Guid.NewGuid();
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(100, 100, 1));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });

        await middleware.InvokeAsync(Context(tenantId, HttpMethods.Post, path), repository);
        var blocked = Context(tenantId, HttpMethods.Post, path);
        await middleware.InvokeAsync(blocked, repository);

        Assert.Equal(1, nextCalls);
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.Equal("60", blocked.Response.Headers.RetryAfter);
    }

    [Fact]
    public async Task InvokeAsync_AiRequest_UsesSeparateBucketFromApiRequests()
    {
        var tenantId = Guid.NewGuid();
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(1, 100, 20));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });

        await middleware.InvokeAsync(Context(tenantId, HttpMethods.Post, "/api/v1/message-assistant/improve"), repository);
        await middleware.InvokeAsync(Context(tenantId, HttpMethods.Get, "/api/v1/reports"), repository);

        Assert.Equal(2, nextCalls);
    }

    [Fact]
    public async Task InvokeAsync_DifferentLoginsInSameTenant_UseSeparateBuckets()
    {
        var tenantId = Guid.NewGuid();
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(1, 100, 20));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });

        await middleware.InvokeAsync(Context(tenantId, HttpMethods.Get, "/api/v1/reports", "user-1"), repository);
        await middleware.InvokeAsync(Context(tenantId, HttpMethods.Get, "/api/v1/reports", "user-2"), repository);

        Assert.Equal(2, nextCalls);
    }

    [Fact]
    public async Task InvokeAsync_NonPostMessagesRoute_UsesApiLimit()
    {
        var tenantId = Guid.NewGuid();
        var nextCalls = 0;
        var repository = new Repository(new TenantRateLimitSettings(2, 0, 20));
        var middleware = new TenantRateLimitMiddleware(_ => { nextCalls++; return Task.CompletedTask; });

        await middleware.InvokeAsync(Context(tenantId, HttpMethods.Get, "/api/v1/messages"), repository);

        Assert.Equal(1, nextCalls);
    }

    private static DefaultHttpContext Context(Guid tenantId, string method, string path, string login = "user-1")
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, login)
            ], "test"));
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private sealed class Repository(TenantRateLimitSettings settings) : ITenantRateLimitRepository
    {
        public int GetCalls { get; private set; }

        public Task<TenantRateLimitSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(settings);
        }

        public Task SaveAsync(Guid tenantId, TenantRateLimitSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
