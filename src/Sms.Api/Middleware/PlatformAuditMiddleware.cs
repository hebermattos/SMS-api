using Sms.Api.Auth;
using System.Security.Claims;

namespace Sms.Api.Middleware;

public sealed class PlatformAuditMiddleware(ILogger<PlatformAuditMiddleware> logger) : IAuditPipelineStep
{
    public Task AuditAsync(AuditPipelineContext audit)
    {
        var action = audit.Action;
        if (action?.ControllerName is not ("Administration" or "AdminTenants" or "AdminAuth" or "SystemLogs"))
            return Task.CompletedTask;

        var context = audit.HttpContext;
        var status = audit.Failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var administrator = context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(PortalSecurity.ContextClaim, PortalSecurity.PlatformContext)
            && context.User.HasClaim(PortalSecurity.RoleClaim, PortalSecurity.AdministratorRole)
            && !context.User.HasClaim(claim => claim.Type == "tenant_id");
        var actor = administrator ? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub") ?? "unauthenticated" : "unauthenticated";
        if (status < 400 && action.ControllerName == "AdminAuth")
            actor = context.Items[PortalSecurity.AdministratorLoginIdentityKey] as string ?? "unauthenticated";
        if (status < 400 && action.ControllerName == "AdminTenants" && !administrator)
            actor = "bootstrap-key";

        // Target IDs are metadata only. Never set TenantId on platform audit events:
        // that would expose administrative activity through the customer log endpoint.
        Guid? targetTenant = Guid.TryParse(context.Request.RouteValues["tenantId"]?.ToString(), out var tenant) ? tenant : null;
        Guid? targetClient = Guid.TryParse(context.Request.RouteValues["clientId"]?.ToString(), out var client) ? client : null;
        Guid? targetAdministrator = Guid.TryParse(context.Request.RouteValues["administratorId"]?.ToString(), out var targetAdmin) ? targetAdmin : null;

        // Platform activity is not tenant activity and must never be stored in UserActivityLogs.
        // Keep only failures here as technical system telemetry.
        if (status >= 400)
            logger.LogError(
                "Platform operation {Action} failed. HTTP {StatusCode}. Target tenant {TargetTenantId}, client {TargetClientId}, administrator {TargetAdministratorId}",
                $"{action.ControllerName}.{action.ActionName}", status, targetTenant, targetClient, targetAdministrator);

        return Task.CompletedTask;
    }
}
