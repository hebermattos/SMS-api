using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Sms.Api.Auth;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    public string CreateAdministrator()
    {
        var settings = options.Value;
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, "platform-administrator"), new Claim(PortalSecurity.AdminClaim, "true")],
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string Create(Guid tenantId, string subject)
    {
        var settings = options.Value;
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, subject), new Claim("tenant_id", tenantId.ToString()) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, expires: DateTime.UtcNow.AddMinutes(settings.ExpirationMinutes), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
