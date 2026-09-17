using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Auth;
using Sms.Application.Auth;

namespace Sms.Api.Controllers;

public sealed record TokenRequest(string ClientId, string ClientSecret);

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(TokenService tokenService, IApiClientRepository clients) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    public async Task<IActionResult> Token([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret)) return Unauthorized();

        var client = await clients.GetActiveByClientIdAsync(request.ClientId, cancellationToken);
        if (client is null || !ClientSecretHasher.Verify(request.ClientSecret, client.SecretHash, client.SecretSalt, client.SecretIterations))
            return Unauthorized();

        return Ok(new
        {
            access_token = tokenService.Create(client.TenantId, client.ClientId),
            token_type = "Bearer"
        });
    }
}
