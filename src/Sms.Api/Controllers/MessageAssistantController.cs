using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Messages;

namespace Sms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/message-assistant")]
public sealed class MessageAssistantController(IMessageAssistant assistant) : ControllerBase
{
    [HttpPost("improve")]
    public async Task<IActionResult> Improve([FromBody] MessageAssistantRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await assistant.ImproveAsync(request.Message, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] MessageAssistantRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await assistant.ValidateAsync(request.Message, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
    }
}

public sealed record MessageAssistantRequest(string Message);
