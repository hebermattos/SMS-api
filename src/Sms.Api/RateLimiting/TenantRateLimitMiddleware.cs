using System.Security.Claims;
using Microsoft.Extensions.Caching.Distributed;
using Sms.Application.Administration;

namespace Sms.Api.RateLimiting;

public sealed class TenantRateLimitMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public async Task InvokeAsync(
        HttpContext context,
        ITenantRateLimitRepository settings,
        IDistributedCache cache)
    {
        var tenantValue = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            await next(context);
            return;
        }

        var login = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(login))
        {
            await next(context);
            return;
        }

        var limits = await settings.GetAsync(tenantId, context.RequestAborted);
        var aiRequest = context.Request.Path.StartsWithSegments("/api/v1/message-assistant");
        var smsRequest = context.Request.Method == HttpMethods.Post
            && (context.Request.Path.Equals("/api/v1/messages", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/api/v1/messages/send")
                || context.Request.Path.StartsWithSegments("/api/v1/messages/bulk"));

        var limit = aiRequest ? limits.OllamaRequestsPerMinute : smsRequest ? limits.SmsPerMinute : limits.RequestsPerMinute;
        var bucket = aiRequest ? "ai" : smsRequest ? "sms" : "api";
        var minute = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        var key = $"rate-limit:{tenantId:N}:{login}:{bucket}:{minute}";

        var count = await IncrementAsync(cache, key, context.RequestAborted);
        if (count > limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            return;
        }

        await next(context);
    }

    private static async Task<long> IncrementAsync(
        IDistributedCache cache,
        string key,
        CancellationToken cancellationToken)
    {
        // IDistributedCache does not expose Redis INCR. A short distributed lock makes the
        // read-modify-write operation safe across API instances while keeping the limiter
        // behind the cache abstraction already used by the application.
        var lockKey = $"{key}:lock";
        var lockValue = Guid.NewGuid().ToString("N");
        var acquired = false;

        for (var attempt = 0; attempt < 20 && !acquired; attempt++)
        {
            var existing = await cache.GetStringAsync(lockKey, cancellationToken);
            if (existing is null)
            {
                await cache.SetStringAsync(lockKey, lockValue, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(2)
                }, cancellationToken);
                acquired = string.Equals(
                    await cache.GetStringAsync(lockKey, cancellationToken),
                    lockValue,
                    StringComparison.Ordinal);
            }

            if (!acquired)
                await Task.Delay(10, cancellationToken);
        }

        if (!acquired)
            throw new InvalidOperationException("Unable to acquire the distributed rate-limit lock.");

        try
        {
            var value = await cache.GetStringAsync(key, cancellationToken);
            var count = long.TryParse(value, out var current) ? current + 1 : 1;
            await cache.SetStringAsync(key, count.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Window + TimeSpan.FromSeconds(5)
            }, cancellationToken);
            return count;
        }
        finally
        {
            if (string.Equals(await cache.GetStringAsync(lockKey, cancellationToken), lockValue, StringComparison.Ordinal))
                await cache.RemoveAsync(lockKey, cancellationToken);
        }
    }
}
