using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Sms.Api.Auth;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    public string CreateAdministrator(Guid administratorId, string username)
    {
        var settings = options.Value;
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, administratorId.ToString()),
                new Claim("admin_username", username),
                new Claim(PortalSecurity.AdminClaim, "true"),
                new Claim(PortalSecurity.ContextClaim, PortalSecurity.PlatformContext),
                new Claim(PortalSecurity.RoleClaim, PortalSecurity.AdministratorRole)
            ],
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string Create(Guid tenantId, string subject)
    {
        var settings = options.Value;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(PortalSecurity.ContextClaim, PortalSecurity.TenantContext),
            new Claim(PortalSecurity.RoleClaim, PortalSecurity.UserRole)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: DateTime.UtcNow.AddMinutes(settings.ExpirationMinutes), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
