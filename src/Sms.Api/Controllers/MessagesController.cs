using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Common;
using Sms.Application.Messages;

namespace Sms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/messages")]
public sealed class MessagesController(ITenantContext tenantContext, ISmsMessageRepository repository, SendSmsService sendSmsService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendSmsRequest request, CancellationToken cancellationToken)
    {
        var result = await sendSmsService.SendAsync(request, cancellationToken);
        return AcceptedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var message = await repository.GetByIdAsync(tenantContext.TenantId, id, cancellationToken);
        return message is null ? NotFound() : Ok(message);
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (skip < 0) return BadRequest("skip must be zero or greater.");
        take = Math.Clamp(take, 1, 200);
        return Ok(await repository.GetHistoryAsync(tenantContext.TenantId, skip, take, cancellationToken));
    }
}
