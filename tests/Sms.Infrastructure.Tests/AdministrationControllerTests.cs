using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Auth;
using Sms.Api.Controllers;
using Sms.Application.Administration;
using Sms.Application.Common;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Tests;

public sealed class AdministrationControllerTests
{
    [Fact]
    public async Task AdministrativeEndpoints_UsePlatformPolicyAndTenantScopedService()
    {
        var attribute = Assert.Single(typeof(AdministrationController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(PortalSecurity.AdminPolicy, attribute.Policy);
        var repo = new AdministrationFakeRepository(); var controller = new AdministrationController(AdministrationServiceTests.Service(repo), new AdministratorAuthenticationService(new AdministratorRepositoryFake()));
        Assert.IsType<OkObjectResult>(await controller.ListTenants());
        Assert.IsType<OkObjectResult>(await controller.GetTenant(repo.Tenant.Id, default));
        Assert.IsType<NoContentResult>(await controller.UpdateTenant(repo.Tenant.Id, new("Company", "UTC", false), default));
        Assert.IsType<OkObjectResult>(await controller.Clients(repo.Tenant.Id));
        Assert.IsType<OkObjectResult>(await controller.CreateClient(repo.Tenant.Id, new("client"), default));
        Assert.IsType<NoContentResult>(await controller.SetClientState(repo.Tenant.Id, repo.ClientId, new(false), default));
        Assert.IsType<OkObjectResult>(await controller.RotateSecret(repo.Tenant.Id, repo.ClientId, default));
        Assert.IsType<OkObjectResult>(controller.Catalog());
        Assert.IsType<OkObjectResult>(await controller.Providers(repo.Tenant.Id, default));
        Assert.IsType<NoContentResult>(await controller.SaveProvider(repo.Tenant.Id, "Twilio", new("account", "+15550000001", true, true, "secret", null), default));
        Assert.Equal(repo.Tenant.Id, repo.SavedProvider!.TenantId);
    }

    private sealed class AdministratorRepositoryFake : IAdministratorRepository
    {
        public Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<AdministratorAccount?>(null);
        public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministratorSummary>>([]);
        public Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AdministratorStateResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult(AdministratorStateResult.NotFound);
        public Task<bool> ResetPasswordAsync(Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    [Fact]
    public async Task Overview_UsesOnlyAuthenticatedTenantAndHandlesMissingTenant()
    {
        var tenant = new TenantContext(); var repository = new OverviewRepository(); var controller = new OverviewController(tenant, repository);
        Assert.IsType<NotFoundResult>(await controller.Get(default));
        Assert.Equal(tenant.TenantId, repository.RequestedTenant);
        repository.Value = new("Company", 1, 2, 1, 0, 0, []);
        Assert.IsType<OkObjectResult>(await controller.Get(default));
    }

    private sealed class TenantContext : ITenantContext { public Guid TenantId { get; } = Guid.NewGuid(); }
    private sealed class OverviewRepository : ITenantPortalRepository
    {
        public Guid RequestedTenant { get; private set; } public TenantOverview? Value { get; set; }
        public Task<TenantOverview?> GetOverviewAsync(Guid tenantId, CancellationToken cancellationToken) { RequestedTenant = tenantId; return Task.FromResult(Value); }
    }
}
