using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Auth;
using Sms.Application.Logs;

namespace Sms.Api.Controllers;

[ApiController]
[Authorize(Policy = PortalSecurity.AdminPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/admin/system-logs")]
public sealed class SystemLogsController(ILogEntryRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) return BadRequest(new { error = "skip must be zero or greater." });
        if (from.HasValue && to.HasValue && from >= to) return BadRequest(new { error = "from must be earlier than to." });

        take = Math.Clamp(take, 1, 200);
        return Ok(await repository.GetSystemAsync(from, to, skip, take, cancellationToken));
    }
}
