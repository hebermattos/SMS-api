using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Middleware;
using Sms.Application.Common;

namespace Sms.Api.Controllers;

public sealed record PageActivityRequest(string Page);

[ApiController]
[Authorize]
[Route("api/v1/activity")]
public sealed class ActivityController(
    ITenantContext tenant,
    ILogger<RequestAuditMiddleware> logger) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> Pages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["overview"] = "Overview",
            ["messages"] = "Messages",
            ["send"] = "Send SMS",
            ["templates"] = "Templates",
            ["users"] = "Users",
            ["reports"] = "Reports",
            ["alerts"] = "Alerts",
            ["opt-outs"] = "Opt-outs",
            ["logs"] = "Activity logs"
        };

    [HttpPost("page")]
    public IActionResult Page([FromBody] PageActivityRequest request)
    {
        if (!Pages.TryGetValue(request.Page, out var page))
            return BadRequest(new { error = "Unknown page." });

        logger.LogInformation(
            "Opened {Page}. Activity type {ActivityType}. Outcome: {Outcome}. User {ActorId}, tenant {TenantId}.",
            page,
            UserActivityKind.PageView.ToString(),
            "Succeeded",
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            tenant.TenantId);

        return NoContent();
    }
}
