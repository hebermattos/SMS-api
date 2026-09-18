using System.Security.Cryptography;
using Sms.Application.Auth;

namespace Sms.Application.Tenants;

public sealed record ProvisionedTenant(Guid TenantId, string Name, string ClientId, string ClientSecret);

public sealed class TenantProvisioningService(ITenantProvisioner provisioner)
{
    public async Task<ProvisionedTenant> CreateAsync(string name, string? requestedClientId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = name.Trim();
        if (normalizedName.Length > 200) throw new ArgumentException("O nome deve ter até 200 caracteres.");
        var tenantId = Guid.NewGuid();
        var clientId = string.IsNullOrWhiteSpace(requestedClientId) ? $"tenant_{tenantId:N}" : requestedClientId.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(clientId, "^[a-zA-Z0-9_-]{1,100}$"))
            throw new ArgumentException("O identificador deve ter até 100 letras, números, hífens ou sublinhados.");
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hashed = ClientSecretHasher.Hash(secret);

        await provisioner.CreateAsync(
            tenantId,
            normalizedName,
            new CreateApiClient(tenantId, clientId, hashed.Hash, hashed.Salt, hashed.Iterations),
            cancellationToken);

        return new ProvisionedTenant(tenantId, normalizedName, clientId, secret);
    }
}
