using Sms.Api.Auth;

namespace Sms.Api.Middleware;

public sealed class PortalLoginAuditMiddleware(ILogger<PortalLoginAuditMiddleware> logger) : IAuditPipelineStep
{
    internal static readonly object IdentityKey = new();

    public Task AuditAsync(AuditPipelineContext audit)
    {
        var action = audit.Action;
        if (action?.ControllerName != "PortalAuth" || action.ActionName != "Token")
            return Task.CompletedTask;

        var context = audit.HttpContext;
        var status = audit.Failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var identity = context.Items[IdentityKey] as PortalLoginIdentity;
        var succeeded = status >= 200 && status < 300 && identity is not null;

        logger.Log(status >= 500 ? LogLevel.Error : succeeded ? LogLevel.Information : LogLevel.Warning,
            "{Activity} Activity type {ActivityType}. HTTP {StatusCode} for user {ActorId}, tenant {TenantId}. Outcome: {Outcome}.",
            succeeded ? "Signed in to the portal." : "Could not sign in to the portal.",
            "Action",
            status,
            succeeded ? identity!.UserId : null,
            succeeded && identity!.Context == PortalSecurity.TenantContext ? identity.TenantId : null,
            succeeded ? "Succeeded" : "Failed");

        return Task.CompletedTask;
    }
}

internal sealed record PortalLoginIdentity(Guid UserId, Guid? TenantId, string Context);
