using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Sms.Api.Controllers;
using Sms.Application.Auth;
using Sms.Application.Tenants;

namespace Sms.Infrastructure.Tests;

public sealed class AdminTenantsControllerTests
{
    [Fact]
    public async Task Create_RejectsMissingOrInvalidAdminKey()
    {
        var controller = CreateController("expected-key");

        Assert.IsType<UnauthorizedResult>(await controller.Create(new CreateTenantRequest("Tenant", null), default));

        controller.Request.Headers["X-Admin-Key"] = "wrong-key";
        Assert.IsType<UnauthorizedResult>(await controller.Create(new CreateTenantRequest("Tenant", null), default));
    }

    [Fact]
    public async Task Create_RejectsBlankTenantName()
    {
        var controller = CreateController("expected-key");
        controller.Request.Headers["X-Admin-Key"] = "expected-key";

        Assert.IsType<BadRequestObjectResult>(await controller.Create(new CreateTenantRequest(" ", null), default));
    }

    [Fact]
    public async Task Create_ReturnsProvisionedTenantAndOneTimeSecret()
    {
        var provisioner = new Provisioner();
        var controller = CreateController("expected-key", provisioner);
        controller.Request.Headers["X-Admin-Key"] = "expected-key";

        var result = Assert.IsType<CreatedResult>(await controller.Create(new CreateTenantRequest("Tenant", "client"), default));

        Assert.Equal($"/api/v1/admin/tenants/{provisioner.TenantId}", result.Location);
        Assert.NotNull(result.Value);
        Assert.Equal("Tenant", provisioner.Name);
        Assert.Equal("client", provisioner.ClientId);
    }

    [Fact]
    public async Task Create_RejectsRequestsWhenAdminKeyIsNotConfigured()
    {
        Assert.IsType<UnauthorizedResult>(await CreateController(null).Create(new CreateTenantRequest("Tenant", null), default));
    }

    [Fact]
    public async Task Create_AcceptsAuthenticatedAdministratorWithoutBootstrapHeader()
    {
        var controller = CreateController(null);
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(Sms.Api.Auth.PortalSecurity.AdminClaim, "true")], "Bearer"));
        Assert.IsType<CreatedResult>(await controller.Create(new CreateTenantRequest("Company", null), default));
    }

    [Fact]
    public async Task Create_RejectsTenantTokensEvenWhenTheyCarryAnAdminClaim()
    {
        var controller = CreateController(null);
        controller.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(Sms.Api.Auth.PortalSecurity.AdminClaim, "true"),
             new System.Security.Claims.Claim("tenant_id", Guid.NewGuid().ToString())], "Bearer"));
        Assert.IsType<UnauthorizedResult>(await controller.Create(new CreateTenantRequest("Company", null), default));
    }

    private static AdminTenantsController CreateController(string? adminKey, Provisioner? provisioner = null)
    {
        var values = new Dictionary<string, string?> { ["Admin:ProvisioningKey"] = adminKey };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var service = new TenantProvisioningService(provisioner ?? new Provisioner());
        return new AdminTenantsController(service, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private sealed class Provisioner : ITenantProvisioner
    {
        public Guid TenantId { get; private set; }
        public string? Name { get; private set; }
        public string? ClientId { get; private set; }

        public Task CreateAsync(Guid id, string name, CreateApiClient client, CancellationToken cancellationToken = default)
        {
            TenantId = id;
            Name = name;
            ClientId = client.ClientId;
            return Task.CompletedTask;
        }
    }
}
