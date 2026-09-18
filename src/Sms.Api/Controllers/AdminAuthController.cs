using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sms.Api.Auth;

namespace Sms.Api.Controllers;

public sealed record AdminTokenRequest(string Key);

[ApiController]
[Route("api/v1/admin/auth")]
public sealed class AdminAuthController(TokenService tokens, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("token")]
    [RequestSizeLimit(4096)]
    public IActionResult Token(AdminTokenRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        return PortalSecurity.ValidateAdminKey(configuration["Admin:ProvisioningKey"], request.Key)
            ? Ok(new { access_token = tokens.CreateAdministrator(), token_type = "Bearer", expires_in = 900 })
            : Unauthorized();
    }
}
