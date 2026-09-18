using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sms.Api.Auth;
using Sms.Application.Auth;

namespace Sms.Api.Controllers;

public sealed record PortalTokenRequest(string Username, string Password, string Context);

[ApiController]
[Route("api/v1/portal/auth")]
public sealed class PortalAuthController(
    IPortalUserRepository users,
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
            || request.Context is not (PortalSecurity.TenantContext or PortalSecurity.PlatformContext))
            return Unauthorized();

        var user = await users.GetActiveByUsernameAsync(
            request.Username.Trim(), request.Context, cancellationToken);

        if (user is null
            || !ClientSecretHasher.Verify(
                request.Password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
            return Unauthorized();

        var token = tokens.CreatePortalUser(
            user.Id, user.Username, user.TenantId, user.Context, user.Role);

        return Ok(new { access_token = token, token_type = "Bearer" });
    }
}
