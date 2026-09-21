using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Messages;

namespace Sms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/message-assistant")]
public sealed class MessageAssistantController(IMessageAssistant assistant, Sms.Application.Common.ITenantContext tenant) : ControllerBase
{
    [HttpPost("improve")]
    public Task<IActionResult> Improve([FromBody] MessageAssistantRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => assistant.ImproveAsync(tenant.TenantId, request.Message, cancellationToken));

    [HttpPost("validate")]
    public Task<IActionResult> Validate([FromBody] MessageAssistantRequest request, CancellationToken cancellationToken) =>
        ExecuteAsync(() => assistant.ValidateAsync(tenant.TenantId, request.Message, cancellationToken));

    private async Task<IActionResult> ExecuteAsync(Func<Task<MessageAssistantResult>> action)
    {
        try { return Ok(await action()); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
    }
}

public sealed record MessageAssistantRequest(string Message);
