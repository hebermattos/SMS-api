using Sms.Api.Auth;
using System.Security.Claims;

namespace Sms.Api.Middleware;

public sealed class PlatformAuditMiddleware(
    IPlatformActivityWriter activities,
    ILogger<PlatformAuditMiddleware> logger) : IAuditPipelineStep
{
    public async Task AuditAsync(AuditPipelineContext audit)
    {
        var action = audit.Action;
        if (action?.ControllerName is not ("Administration" or "AdminTenants" or "AdminAuth" or "SystemLogs"))
            return;

        var context = audit.HttpContext;
        var status = audit.Failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var administrator = context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(PortalSecurity.ContextClaim, PortalSecurity.PlatformContext)
            && context.User.HasClaim(PortalSecurity.RoleClaim, PortalSecurity.AdministratorRole)
            && !context.User.HasClaim(claim => claim.Type == "tenant_id");

        var actor = administrator
            ? context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub")
            : null;
        if (status < 400 && action.ControllerName == "AdminAuth")
            actor = context.Items[PortalSecurity.AdministratorLoginIdentityKey] as string;
        if (status < 400 && action.ControllerName == "AdminTenants" && !administrator)
            actor = "bootstrap-key";

        var target = GetTarget(context);
        var succeeded = status < 400;

        if (actor is not null)
        {
            await activities.WriteAsync(new PlatformActivity(
                actor,
                UserActivityKind.Action.ToString(),
                $"{action.ControllerName}.{action.ActionName}",
                target.Type,
                target.Id,
                succeeded
                    ? $"Completed platform operation {action.ActionName}."
                    : $"Could not complete platform operation {action.ActionName}.",
                succeeded ? "Succeeded" : "Failed"),
                context.RequestAborted);
        }

        if (!succeeded)
            logger.LogError(
                "Platform operation {Action} failed. HTTP {StatusCode}. Target {TargetType} {TargetId}",
                $"{action.ControllerName}.{action.ActionName}", status, target.Type, target.Id);
    }

    private static (string? Type, string? Id) GetTarget(HttpContext context)
    {
        foreach (var (route, type) in new[]
        {
            ("tenantId", "Tenant"),
            ("clientId", "ApiClient"),
            ("administratorId", "Administrator")
        })
        {
            var value = context.Request.RouteValues[route]?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return (type, value);
        }

        return (null, null);
    }
}
