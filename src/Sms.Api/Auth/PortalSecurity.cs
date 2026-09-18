using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Sms.Application.Auth;

namespace Sms.Api.Auth;

public static class PortalSecurity
{
    public const string AdminPolicy = "PlatformAdmin";
    public const string AdminClaim = "platform_admin";

    public static void ConfigureAuthorization(AuthorizationOptions options)
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
            .RequireAssertion(context => Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out _)
                && !context.User.HasClaim(AdminClaim, "true")).Build();
        options.AddPolicy(AdminPolicy, policy => policy.RequireAuthenticatedUser().RequireClaim(AdminClaim, "true")
            .RequireAssertion(context => !context.User.HasClaim(x => x.Type == "tenant_id")));
    }

    public static bool ValidateAdminKey(string? expected, string? supplied) =>
        !string.IsNullOrWhiteSpace(expected) && !string.IsNullOrWhiteSpace(supplied)
        && supplied.Length <= 1024
        && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));

    public static async Task ValidateTenantAsync(TokenValidatedContext context)
    {
        var tenantClaim = context.Principal?.FindFirstValue("tenant_id");
        if (tenantClaim is null) return; // Admin tokens are authorized separately and carry no tenant claim.
        var subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(tenantClaim, out var tenantId) || string.IsNullOrWhiteSpace(subject))
        {
            context.Fail("Invalid tenant identity.");
            return;
        }
        var clients = context.HttpContext.RequestServices.GetRequiredService<IApiClientRepository>();
        var client = await clients.GetActiveByClientIdAsync(subject, context.HttpContext.RequestAborted);
        if (client is null || client.TenantId != tenantId) context.Fail("Inactive tenant or client.");
    }
}
