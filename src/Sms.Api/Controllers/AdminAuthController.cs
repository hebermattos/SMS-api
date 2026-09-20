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
public sealed class AdminAuthController(
    RefreshTokenService refreshTokens,
    AdministratorAuthenticationService authentication) : ControllerBase
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

        var issued = await refreshTokens.IssueAsync(
            account.Id, account.Username, null, PortalSecurity.PlatformContext,
            PortalSecurity.AdministratorRole, true, cancellationToken);
        HttpContext.Items[PortalSecurity.AdministratorLoginIdentityKey] = account.Id.ToString();
        return Ok(new
        {
            access_token = issued.AccessToken,
            refresh_token = issued.RefreshToken,
            token_type = "Bearer",
            expires_in = issued.ExpiresIn
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("refresh")]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var issued = await refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);
        return issued is null ? Unauthorized() : Ok(new
        {
            access_token = issued.AccessToken,
            refresh_token = issued.RefreshToken,
            token_type = "Bearer",
            expires_in = issued.ExpiresIn
        });
    }
}
