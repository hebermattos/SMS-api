using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Common;
using Sms.Application.Logs;

namespace Sms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/logs")]
public sealed class LogsController(ITenantContext tenantContext, ILogEntryRepository repository, ITenantTimeZoneProvider? timeZones = null) : ControllerBase
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
        var range = TenantDateRange.ToUtc(timeZones is null ? TimeZoneInfo.Utc : await timeZones.GetAsync(tenantContext.TenantId, cancellationToken), from, to);
        var rows = await repository.GetActivityAsync(tenantContext.TenantId, range.From, range.To, skip, take, cancellationToken);
        var zone = timeZones is null ? TimeZoneInfo.Utc : await timeZones.GetAsync(tenantContext.TenantId, cancellationToken);
        return Ok(rows.Select(row => row with { Timestamp = TimeZoneInfo.ConvertTime(row.Timestamp, zone) }));
    }
}
