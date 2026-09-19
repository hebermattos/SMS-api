using Microsoft.AspNetCore.Mvc.Controllers;

namespace Sms.Api.Middleware;

public sealed class ClientLoginAuditMiddleware(RequestDelegate next, ILogger<ClientLoginAuditMiddleware> logger)
{
    internal static readonly object IdentityKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        if (action?.ControllerName != "Auth" || action.ActionName != "Token")
        {
            await next(context);
            return;
        }

        var failed = false;
        try { await next(context); }
        catch { failed = true; throw; }
        finally
        {
            var status = failed ? 500 : context.Response.StatusCode;
            var identity = context.Items[IdentityKey] as ClientLoginIdentity;
            var succeeded = status >= 200 && status < 300 && identity is not null;
            var activity = succeeded ? "Signed in to the API." : "Could not sign in to the API.";
            logger.Log(status >= 500 ? LogLevel.Error : succeeded ? LogLevel.Information : LogLevel.Warning,
                "{Activity}",
                activity, succeeded ? "Succeeded" : "Failed", succeeded ? identity!.ClientId : null,
                succeeded ? identity!.TenantId : (Guid?)null, status);
        }
    }
}

internal sealed record ClientLoginIdentity(Guid TenantId, string ClientId);
