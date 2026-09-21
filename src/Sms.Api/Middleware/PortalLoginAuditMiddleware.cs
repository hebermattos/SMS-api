using Sms.Api.Auth;

namespace Sms.Api.Middleware;

public sealed class PortalLoginAuditMiddleware(IUserActivityWriter activities) : IAuditPipelineStep
{
    internal static readonly object IdentityKey = new();

    public async Task AuditAsync(AuditPipelineContext audit)
    {
        var action = audit.Action;
        if (action?.ControllerName != "PortalAuth" || action.ActionName != "Token")
            return;

        var context = audit.HttpContext;
        var identity = context.Items[IdentityKey] as PortalLoginIdentity;
        if (identity is null || identity.Context != PortalSecurity.TenantContext || identity.TenantId is null)
            return;

        var status = audit.Failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var succeeded = status >= 200 && status < 300;

        await activities.WriteAsync(new UserActivity(
            identity.TenantId.Value,
            identity.UserId.ToString(),
            UserActivityKind.Action.ToString(),
            "PortalSignedIn",
            "User",
            identity.UserId.ToString(),
            succeeded ? "Signed in to the portal." : "Could not sign in to the portal.",
            succeeded ? "Succeeded" : "Failed"),
            context.RequestAborted);
    }
}

internal sealed record PortalLoginIdentity(Guid UserId, Guid? TenantId, string Context);
