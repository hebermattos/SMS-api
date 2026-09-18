using Microsoft.AspNetCore.Mvc.Controllers;
using Sms.Api.Auth;

namespace Sms.Api.Middleware;

public sealed class PlatformAuditMiddleware(RequestDelegate next, ILogger<PlatformAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.ControllerName is not ("Administration" or "AdminTenants" or "AdminAuth"))
        {
            await next(context);
            return;
        }

        var failed = false;
        try
        {
            await next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            var status = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
            var administrator = context.User.Identity?.IsAuthenticated == true
                && context.User.HasClaim(PortalSecurity.AdminClaim, "true")
                && !context.User.HasClaim(claim => claim.Type == "tenant_id");
            var actor = administrator ? "platform-administrator" : "unauthenticated";
            if (status < 400 && action.ControllerName == "AdminAuth") actor = "platform-administrator";
            if (status < 400 && action.ControllerName == "AdminTenants" && !administrator) actor = "bootstrap-key";

            // Target IDs are metadata only. Never set TenantId on platform audit events:
            // that would expose administrative activity through the customer log endpoint.
            Guid? targetTenant = Guid.TryParse(context.Request.RouteValues["tenantId"]?.ToString(), out var tenant) ? tenant : null;
            Guid? targetClient = Guid.TryParse(context.Request.RouteValues["clientId"]?.ToString(), out var client) ? client : null;
            logger.Log(status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information,
                "Platform action {Action} by {Actor}: {Outcome}, HTTP {StatusCode}. Target tenant {TargetTenantId}, client {TargetClientId}",
                $"{action.ControllerName}.{action.ActionName}", actor,
                status < 400 ? "Succeeded" : "Failed", status, targetTenant, targetClient);
        }
    }
}
