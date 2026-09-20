using System.Collections.Concurrent;
using System.Security.Claims;
using Sms.Application.Administration;

namespace Sms.Api.RateLimiting;

public sealed class TenantRateLimitMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<string, Counter> Counters = new();

    public async Task InvokeAsync(HttpContext context, ITenantRateLimitRepository settings)
    {
        var tenantValue = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            await next(context);
            return;
        }

        var limits = await settings.GetAsync(tenantId, context.RequestAborted);
        var smsRequest = context.Request.Method == HttpMethods.Post
            && (context.Request.Path.Equals("/api/v1/messages", StringComparison.OrdinalIgnoreCase)
                || context.Request.Path.StartsWithSegments("/api/v1/messages/send")
                || context.Request.Path.StartsWithSegments("/api/v1/messages/bulk"));
        var limit = smsRequest ? limits.SmsPerMinute : limits.RequestsPerMinute;
        var key = $"{tenantId:N}:{(smsRequest ? "sms" : "api")}";
        var now = DateTimeOffset.UtcNow;
        var counter = Counters.GetOrAdd(key, _ => new Counter(now));

        lock (counter)
        {
            if (now - counter.StartedAt >= TimeSpan.FromMinutes(1))
            {
                counter.StartedAt = now;
                counter.Count = 0;
            }

            counter.Count++;
            if (counter.Count > limit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = "60";
                return;
            }
        }

        await next(context);
    }

    private sealed class Counter(DateTimeOffset startedAt)
    {
        public DateTimeOffset StartedAt { get; set; } = startedAt;
        public int Count { get; set; }
    }
}
