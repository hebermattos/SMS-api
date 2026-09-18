using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sms.Application.Administration;
using Sms.Application.Auth;

namespace Sms.Api.Filters;

public sealed class PortalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var response = context.Exception switch
        {
            ArgumentException exception => (400, exception.Message),
            KeyNotFoundException => (404, "Record not found."),
            LastActiveAdministratorException => (409, "The last active administrator cannot be deactivated."),
            AdministratorConflictException => (409, "An administrator with this username already exists."),
            AdministrationConflictException => (409, "A record with these identifiers already exists."),
            _ => (0, string.Empty)
        };
        if (response.Item1 == 0) return;
        context.Result = new ObjectResult(new { error = response.Item2 }) { StatusCode = response.Item1 };
        context.ExceptionHandled = true;
    }
}
