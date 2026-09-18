using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sms.Api.Auth;
using System.ComponentModel.DataAnnotations;
using Sms.Application.Auth;

namespace Sms.Api.Controllers;

public sealed record AdminTokenRequest(
    [Required, StringLength(100)] string Username,
    [Required, StringLength(128)] string Password);

[ApiController]
[Route("api/v1/admin/auth")]
public sealed class AdminAuthController(TokenService tokens, AdministratorAuthenticationService authentication) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("token")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Token(AdminTokenRequest request, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var account = await authentication.AuthenticateAsync(request.Username, request.Password, cancellationToken);
        if (account is null) return Unauthorized();
        var token = tokens.CreateAdministrator(account.Id, account.Username);
        HttpContext.Items[PortalSecurity.AdministratorLoginIdentityKey] = account.Id.ToString();
        return Ok(new { access_token = token, token_type = "Bearer", expires_in = 900 });
    }
}
