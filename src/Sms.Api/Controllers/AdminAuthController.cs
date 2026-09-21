using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sms.Api.Auth;
using Sms.Application.Auth;

namespace Sms.Api.Controllers;

public sealed record AdminTokenRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Username,
    [Required, StringLength(128, MinimumLength = 1)] string Password);

[ApiController]
[Route("api/v1/admin/auth")]
public sealed class AdminAuthController(
    IPortalUserRepository users,
    RefreshTokenService refreshTokens) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("token")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Token(AdminTokenRequest request, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100
            || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
            return Unauthorized();

        var account = await users.GetActiveByUsernameAsync(
            request.Username.Trim(), PortalSecurity.PlatformContext, null, cancellationToken);
        if (account is null
            || account.Role != PortalSecurity.AdministratorRole
            || !ClientSecretHasher.Verify(request.Password, account.PasswordHash, account.PasswordSalt, account.PasswordIterations))
            return Unauthorized();

        var issued = await refreshTokens.IssueAsync(
            account.Id, account.Username, null, PortalSecurity.PlatformContext,
            PortalSecurity.AdministratorRole, cancellationToken);
        HttpContext.Items[PortalSecurity.AdministratorLoginIdentityKey] = account.Id.ToString();
        return Ok(new { access_token = issued.AccessToken, refresh_token = issued.RefreshToken, token_type = "Bearer", expires_in = issued.ExpiresIn });
    }

    [Authorize(Policy = PortalSecurity.AdminPolicy)]
    [HttpPost("logout")]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        await refreshTokens.RevokeAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("refresh")]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var issued = await refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);
        return issued is null ? Unauthorized() : Ok(new { access_token = issued.AccessToken, refresh_token = issued.RefreshToken, token_type = "Bearer", expires_in = issued.ExpiresIn });
    }
}
