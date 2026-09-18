using System.Diagnostics;
using System.Security.Claims;

namespace Sms.Api.Middleware;

public sealed class RequestAuditMiddleware(RequestDelegate next, ILogger<RequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var tenantId = context.User.FindFirstValue("tenant_id");
            if (Guid.TryParse(tenantId, out var parsedTenantId))
            {
                logger.LogInformation(
                    "HTTP request completed for tenant {TenantId}: {RequestMethod} {RequestPath} returned {StatusCode} in {ElapsedMilliseconds} ms",
                    parsedTenantId,
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
    }
}
