using Sms.Application.Auth;
using Sms.Application.Tenants;

namespace Sms.Infrastructure.Tests;

public sealed class TenantProvisioningServiceTests
{
    [Fact]
    public async Task CreateAsync_GeneratesCredentialsAndHashesSecret()
    {
        var provisioner = new FakeProvisioner();
        var service = new TenantProvisioningService(provisioner);
        var result = await service.CreateAsync(" Example Tenant ", " client-one ");

        Assert.Equal("Example Tenant", result.Name);
        Assert.Equal("client-one", result.ClientId);
        Assert.NotEmpty(result.ClientSecret);
        Assert.NotNull(provisioner.Client);
        Assert.Equal(result.TenantId, provisioner.Client!.TenantId);
        Assert.True(ClientSecretHasher.Verify(result.ClientSecret, provisioner.Client.SecretHash, provisioner.Client.SecretSalt, provisioner.Client.SecretIterations));
    }

    [Fact]
    public async Task CreateAsync_GeneratesClientIdWhenNotProvided()
    {
        var service = new TenantProvisioningService(new FakeProvisioner());
        var result = await service.CreateAsync("Tenant");
        Assert.StartsWith("tenant_", result.ClientId);
        Assert.Contains(result.TenantId.ToString("N"), result.ClientId);
    }

    [Fact]
    public async Task CreateAsync_RejectsBlankName()
    {
        var service = new TenantProvisioningService(new FakeProvisioner());
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(" "));
    }

    private sealed class FakeProvisioner : ITenantProvisioner
    {
        public CreateApiClient? Client { get; private set; }
        public Task CreateAsync(Guid tenantId, string name, CreateApiClient client, CancellationToken cancellationToken = default)
        {
            Client = client;
            return Task.CompletedTask;
        }
    }
}
