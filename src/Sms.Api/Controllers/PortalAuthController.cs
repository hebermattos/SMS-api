using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sms.Api.Auth;
using Sms.Application.Auth;

namespace Sms.Api.Controllers;

public sealed record PortalTokenRequest(string Username, string Password, string Context, string? TenantCode = null);

[ApiController]
[Route("api/v1/portal/auth")]
public sealed class PortalAuthController(
    IPortalUserRepository users,
    AdministratorAuthenticationService administrators,
    TokenService tokens) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [RequestSizeLimit(4096)]
    [HttpPost("token")]
    public async Task<IActionResult> Token(
        [FromBody] PortalTokenRequest request,
        CancellationToken cancellationToken)
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

        var tenantCode = request.Context == PortalSecurity.TenantContext
            ? request.TenantCode!.Trim()
            : null;
        var user = await users.GetActiveByUsernameAsync(
            request.Username.Trim(), request.Context, tenantCode, cancellationToken);

        if (user is not null
            && ClientSecretHasher.Verify(
                request.Password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            var portalToken = tokens.CreatePortalUser(
                user.Id, user.Username, user.TenantId, user.Context, user.Role);
            return Ok(new { access_token = portalToken, token_type = "Bearer" });
        }

        if (request.Context == PortalSecurity.PlatformContext)
        {
            var administrator = await administrators.AuthenticateAsync(
                request.Username.Trim(), request.Password, cancellationToken);
            if (administrator is not null)
            {
                var administratorToken = tokens.CreateAdministrator(
                    administrator.Id, administrator.Username);
                return Ok(new { access_token = administratorToken, token_type = "Bearer" });
            }
        }

        return Unauthorized();
    }
}
