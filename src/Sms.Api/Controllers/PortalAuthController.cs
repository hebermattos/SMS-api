using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sms.Api.Auth;
using Sms.Application.Auth;

namespace Sms.Api.Controllers;

public sealed record PortalTokenRequest(string Username, string Password, string Context, string? TenantCode = null);
public sealed record RefreshTokenRequest(string RefreshToken);

[ApiController]
[Route("api/v1/portal/auth")]
public sealed class PortalAuthController(
    IPortalUserRepository users,
    AdministratorAuthenticationService administrators,
    RefreshTokenService refreshTokens) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [RequestSizeLimit(4096)]
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] PortalTokenRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";

        if (string.IsNullOrWhiteSpace(request.Username)
            || request.Username.Length > 100
            || string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length > 128
            || request.Context is not (PortalSecurity.TenantContext or PortalSecurity.PlatformContext)
            || (request.Context == PortalSecurity.TenantContext
                && (string.IsNullOrWhiteSpace(request.TenantCode) || request.TenantCode.Length > 100)))
            return Unauthorized();

        var tenantCode = request.Context == PortalSecurity.TenantContext ? request.TenantCode!.Trim() : null;
        var user = await users.GetActiveByUsernameAsync(
            request.Username.Trim(), request.Context, tenantCode, cancellationToken);

        if (user is not null
            && ClientSecretHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            var issued = await refreshTokens.IssueAsync(
                user.Id, user.Username, user.TenantId, user.Context, user.Role,
                cancellationToken: cancellationToken);
            return Ok(ToResponse(issued));
        }

        if (request.Context == PortalSecurity.PlatformContext)
        {
            var administrator = await administrators.AuthenticateAsync(
                request.Username.Trim(), request.Password, cancellationToken);
            if (administrator is not null)
            {
                var issued = await refreshTokens.IssueAsync(
                    administrator.Id, administrator.Username, null,
                    PortalSecurity.PlatformContext, PortalSecurity.AdministratorRole, true, cancellationToken);
                return Ok(ToResponse(issued));
            }
        }

        return Unauthorized();
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [RequestSizeLimit(2048)]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var issued = await refreshTokens.RotateAsync(request.RefreshToken, cancellationToken);
        return issued is null ? Unauthorized() : Ok(ToResponse(issued));
    }

    private static object ToResponse(IssuedTokens issued) => new
    {
        access_token = issued.AccessToken,
        refresh_token = issued.RefreshToken,
        token_type = "Bearer",
        expires_in = issued.ExpiresIn
    };
}
