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

        var description = UserActivityMessageFormatter.Format(
            audit.Action?.ControllerName, audit.Action?.ActionName);
        if (description is null)
            return Task.CompletedTask;

        var status = audit.Failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var activity = status < 400 ? description.Success : description.Failure;

        logger.LogInformation(
            "{Activity} Activity type {ActivityType}. HTTP {StatusCode} in {ElapsedMilliseconds} ms for user {ActorId}, tenant {TenantId}.",
            activity,
            description.Kind.ToString(),
            status,
            Stopwatch.GetElapsedTime(audit.StartedTimestamp).TotalMilliseconds,
            context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"),
            parsedTenantId);

        return Task.CompletedTask;
    }
}
