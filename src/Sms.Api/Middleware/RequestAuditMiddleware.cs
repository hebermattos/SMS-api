using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Sms.Api.Middleware;

public sealed class RequestAuditMiddleware(RequestDelegate next, ILogger<RequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        try
        {
            await next(context);
        }
        finally
        {
            var tenantId = context.User.FindFirstValue("tenant_id");
            if (context.User.Identity?.IsAuthenticated == true && Guid.TryParse(tenantId, out var parsedTenantId))
            {
                var activity = UserActivityMessageFormatter.Format(
                    action?.ControllerName, action?.ActionName, context.Response.StatusCode);
                logger.LogInformation(
                    "{Activity} {RequestMethod} {RequestPath} returned HTTP {StatusCode} in {ElapsedMilliseconds} ms for client {ClientId}, tenant {TenantId}.",
                    activity,
                    context.Request.Method,
                    context.Request.Path.Value,
                    context.Response.StatusCode,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"),
                    parsedTenantId
                    );
            }
        }
    }
}
