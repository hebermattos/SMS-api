using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sms.Api.Auth;
using Sms.Api.Controllers;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Tests;

public sealed class PortalAuthControllerTests
{
    private const string Password = "administrator-password";
    private readonly TokenService tokens = new(Options.Create(new JwtOptions
    {
        Key = "local-test-key-at-least-32-characters",
        Issuer = "test",
        Audience = "test"
    }));

    [Fact]
    public async Task ValidTenantPortalUserReceivesContextualToken()
    {
        var user = Account("tenant", "user");
        var controller = Controller(new Users(user), new AdminRepository());
        var result = await controller.Token(new(" portal ", Password, "tenant"), CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(response.Value);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task PlatformAdministratorCanUsePlatformContext()
    {
        var adminRepo = new AdminRepository();
        var authentication = new AdministratorAuthenticationService(adminRepo);
        await authentication.CreateAsync("admin", "admin@example.com", Password);
        var controller = Controller(new Users(null), adminRepo, authentication);

        var result = await controller.Token(new("ADMIN", Password, "platform"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Theory]
    [InlineData("", "tenant")]
    [InlineData("user", "")]
    [InlineData("user", "invalid")]
    public async Task InvalidOrUnknownCredentialsAreRejected(string username, string context)
    {
        var controller = Controller(new Users(null), new AdminRepository());
        var result = await controller.Token(new(username, Password, context), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result);
    }

    private PortalAuthController Controller(Users users, AdminRepository repository, AdministratorAuthenticationService? authentication = null)
    {
        return new(users, authentication ?? new AdministratorAuthenticationService(repository), tokens)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static PortalUserAccount Account(string context, string role) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "portal", "portal@example.com",
            ClientSecretHasher.Hash(Password).Hash, ClientSecretHasher.Hash(Password).Salt, ClientSecretHasher.DefaultIterations, context, role, true);

    private sealed class Users(PortalUserAccount? account) : IPortalUserRepository
    {
        public Task<PortalUserAccount?> GetActiveByUsernameAsync(string username, string context, CancellationToken cancellationToken = default) =>
            Task.FromResult(account is not null && account.Context == context ? account : null);
        public Task<PortalUserAccount?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<PortalUserAccount?>(null);
    }

    private sealed class AdminRepository : IAdministratorRepository
    {
        public AdministratorAccount? Account { get; private set; }
        public Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult(Account?.Username.Equals(username, StringComparison.OrdinalIgnoreCase) == true ? Account : null);
        public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Account?.Id == id && Account.IsActive);
        public Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministratorSummary>>([]);
        public Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default) { Account = account; return Task.CompletedTask; }
        public Task<AdministratorStateResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult(AdministratorStateResult.Updated);
        public Task<bool> ResetPasswordAsync(Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
