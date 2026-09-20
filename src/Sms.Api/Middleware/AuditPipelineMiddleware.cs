using Microsoft.AspNetCore.Mvc.Controllers;

namespace Sms.Api.Middleware;

public sealed record AuditPipelineContext(
    HttpContext HttpContext,
    ControllerActionDescriptor? Action,
    bool Failed);

public interface IAuditPipelineStep
{
    Task AuditAsync(AuditPipelineContext context);
}

public sealed class AuditPipelineMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IEnumerable<IAuditPipelineStep> steps)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
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
            var auditContext = new AuditPipelineContext(context, action, failed);
            foreach (var step in steps)
                await step.AuditAsync(auditContext);
        }
    }
}
