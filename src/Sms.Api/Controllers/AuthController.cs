using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Auth;

namespace Sms.Api.Controllers;

public sealed record TokenRequest(Guid TenantId, string ClientId, string ClientSecret);

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(TokenService tokenService, IConfiguration configuration) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    public IActionResult Token([FromBody] TokenRequest request)
    {
        var configuredId = configuration["Auth:ClientId"];
        var configuredSecret = configuration["Auth:ClientSecret"];
        if (string.IsNullOrEmpty(configuredId) || string.IsNullOrEmpty(configuredSecret) ||
            !string.Equals(request.ClientId, configuredId, StringComparison.Ordinal) ||
            !string.Equals(request.ClientSecret, configuredSecret, StringComparison.Ordinal)) return Unauthorized();

        return Ok(new { access_token = tokenService.Create(request.TenantId, request.ClientId), token_type = "Bearer" });
    }
}
