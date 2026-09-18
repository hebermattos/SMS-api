using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sms.Application.Administration;

namespace Sms.Api.Filters;

public sealed class PortalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var response = context.Exception switch
        {
            ArgumentException exception => (400, exception.Message),
            KeyNotFoundException => (404, "Cadastro não encontrado."),
            AdministrationConflictException => (409, "Já existe um cadastro com estes identificadores."),
            _ => (0, string.Empty)
        };
        if (response.Item1 == 0) return;
        context.Result = new ObjectResult(new { error = response.Item2 }) { StatusCode = response.Item1 };
        context.ExceptionHandled = true;
    }
}
