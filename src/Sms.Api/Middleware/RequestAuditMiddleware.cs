using System.Diagnostics;
using System.Security.Claims;

namespace Sms.Api.Middleware;

public sealed class RequestAuditMiddleware(ILogger<RequestAuditMiddleware> logger) : IAuditPipelineStep
{
    public Task AuditAsync(AuditPipelineContext audit)
    {
        var context = audit.HttpContext;
        var tenantId = context.User.FindFirstValue("tenant_id");
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(tenantId, out var parsedTenantId))
            return Task.CompletedTask;

        var activity = UserActivityMessageFormatter.Format(
            audit.Action?.ControllerName, audit.Action?.ActionName, context.Response.StatusCode);

        logger.LogInformation(
            "{Activity} {RequestMethod} {RequestPath} returned HTTP {StatusCode} for client {ClientId}, tenant {TenantId}.",
            activity,
            context.Request.Method,
            context.Request.Path.Value,
            audit.Failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"),
            parsedTenantId);

        return Task.CompletedTask;
    }
}
