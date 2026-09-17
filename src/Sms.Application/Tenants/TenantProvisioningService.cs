using System.Security.Cryptography;
using Sms.Application.Auth;

namespace Sms.Application.Tenants;

public sealed record ProvisionedTenant(Guid TenantId, string Name, string ClientId, string ClientSecret);

public sealed class TenantProvisioningService(ITenantRepository tenants, IApiClientRepository clients)
{
    public async Task<ProvisionedTenant> CreateAsync(string name, string? requestedClientId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var tenantId = Guid.NewGuid();
        var clientId = string.IsNullOrWhiteSpace(requestedClientId) ? $"tenant_{tenantId:N}" : requestedClientId.Trim();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hashed = ClientSecretHasher.Hash(secret);

        await tenants.CreateAsync(tenantId, name.Trim(), cancellationToken);
        await clients.CreateAsync(new CreateApiClient(tenantId, clientId, hashed.Hash, hashed.Salt, hashed.Iterations), cancellationToken);

        return new ProvisionedTenant(tenantId, name.Trim(), clientId, secret);
    }
}
