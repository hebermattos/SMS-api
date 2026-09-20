namespace Sms.Api.Middleware;

public sealed class ClientLoginAuditMiddleware(ILogger<ClientLoginAuditMiddleware> logger) : IAuditPipelineStep
{
    internal static readonly object IdentityKey = new();

    public Task AuditAsync(AuditPipelineContext audit)
    {
        var action = audit.Action;
        if (action?.ControllerName != "Auth" || action.ActionName != "Token")
            return Task.CompletedTask;

        var context = audit.HttpContext;
        var status = audit.Failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
        var identity = context.Items[IdentityKey] as ClientLoginIdentity;
        var succeeded = status >= 200 && status < 300 && identity is not null;
        var activity = succeeded ? "Signed in to the API." : "Could not sign in to the API.";

        logger.Log(status >= 500 ? LogLevel.Error : succeeded ? LogLevel.Information : LogLevel.Warning,
            "{Activity} HTTP {StatusCode} for client {ClientId}, tenant {TenantId}. Outcome: {Outcome}.",
            activity, status, succeeded ? identity!.ClientId : null,
            succeeded ? identity!.TenantId : (Guid?)null, succeeded ? "Succeeded" : "Failed");

        return Task.CompletedTask;
    }
}

internal sealed record ClientLoginIdentity(Guid TenantId, string ClientId);
