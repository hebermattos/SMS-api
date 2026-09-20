using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sms.Api.Auth;
using Sms.Api.Controllers;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Tests;

public sealed class AdministratorAuthenticationTests
{
    private const string Password = "local-admin-test-password";

    [Fact]
    public async Task PasswordsUseRandomSaltAndAreNeverStoredInPlaintext()
    {
        var repository = new Administrators();
        var service = new AdministratorAuthenticationService(repository);
        var id = await service.CreateAsync(" admin ", "admin@example.com", Password);
        var first = repository.Account!;
        Assert.Equal(id, first.Id); Assert.Equal("admin", first.Username);
        Assert.Equal(600000, first.PasswordIterations); Assert.Equal(32, first.PasswordHash.Length);
        Assert.True(ClientSecretHasher.Verify(Password, first.PasswordHash, first.PasswordSalt, first.PasswordIterations));
        await service.CreateAsync("another-admin", "another@example.com", Password);
        Assert.NotEqual(first.PasswordSalt, repository.Account!.PasswordSalt);
        Assert.NotEqual(first.PasswordHash, repository.Account.PasswordHash);
    }

    [Theory]
    [InlineData("admin", "too-short")]
    [InlineData("a", Password)]
    [InlineData("bad username", Password)]
    [InlineData("", Password)]
    public async Task ProvisioningRejectsInvalidCredentials(string username, string password)
    {
        var repository = new Administrators();
        await Assert.ThrowsAsync<ArgumentException>(() => new AdministratorAuthenticationService(repository).CreateAsync(username, "admin@example.com", password));
        Assert.Null(repository.Account);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task LoginRequiresCorrectPasswordAndActiveAccount(bool active, bool correctPassword, bool allowed)
    {
        var repository = new Administrators();
        var authentication = new AdministratorAuthenticationService(repository);
        await authentication.CreateAsync("admin", "admin@example.com", Password);
        repository.Account = repository.Account! with { IsActive = active };
        var tokens = new TokenService(Options.Create(new JwtOptions { Key = "local-test-key-at-least-32-characters", Issuer = "test", Audience = "test" }));
        var controller = new AdminAuthController(CreateRefreshTokens(tokens, repository), authentication) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var result = await controller.Token(new(" admin ", correctPassword ? Password : "wrong"));
        if (allowed)
        {
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(repository.Account.Id.ToString(), controller.HttpContext.Items[PortalSecurity.AdministratorLoginIdentityKey]);
        }
        else
        {
            Assert.IsType<UnauthorizedResult>(result);
            Assert.False(controller.HttpContext.Items.ContainsKey(PortalSecurity.AdministratorLoginIdentityKey));
        }
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task UnknownAndInvalidCredentialsAreRejected()
    {
        var authentication = new AdministratorAuthenticationService(new Administrators());
        Assert.Null(await authentication.AuthenticateAsync("unknown", Password));
        Assert.Null(await authentication.AuthenticateAsync("", Password));
        Assert.Null(await authentication.AuthenticateAsync("admin", ""));
        Assert.Null(await authentication.AuthenticateAsync(new string('x', 101), Password));
        Assert.Null(await authentication.AuthenticateAsync("admin", new string('x', 129)));
        await Assert.ThrowsAsync<ArgumentException>(() => authentication.CreateAsync("admin", "admin@example.com", new string('x', 129)));
    }

    [Fact]
    public async Task AdministratorsCanBeListedDeactivatedAndHavePasswordsReset()
    {
        var repository = new Administrators();
        var service = new AdministratorAuthenticationService(repository);
        var id = await service.CreateAsync("admin", "admin@example.com", Password);
        Assert.Single(await service.ListAsync());
        await service.ResetPasswordAsync(id, "replacement-password");
        Assert.NotNull(await service.AuthenticateAsync("admin", "replacement-password"));
        await service.SetActiveAsync(id, false);
        Assert.False(repository.Account!.IsActive);
    }

    [Fact]
    public async Task LastActiveAdministratorCannotBeDeactivated()
    {
        var repository = new Administrators { StateResult = AdministratorStateResult.LastActive };
        var service = new AdministratorAuthenticationService(repository);
        await Assert.ThrowsAsync<LastActiveAdministratorException>(() => service.SetActiveAsync(Guid.NewGuid(), false));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task TokenValidationRechecksAdministratorStatus(bool active)
    {
        var account = new AdministratorAccount(Guid.NewGuid(), "admin", "admin@example.com", [], [], 600000, active);
        var repository = new Administrators { Account = account };
        var http = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton<IAdministratorRepository>(repository).BuildServiceProvider() };
        var context = new TokenValidatedContext(http, new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), new JwtBearerOptions())
        {
            Principal = new(new ClaimsIdentity([new Claim(PortalSecurity.AdminClaim, "true"), new Claim(ClaimTypes.NameIdentifier, account.Id.ToString())], "Bearer"))
        };
        await PortalSecurity.ValidateTenantAsync(context);
        Assert.Equal(!active, context.Result?.Failure is not null);
        context.Principal!.AddIdentity(new ClaimsIdentity([new Claim("tenant_id", Guid.NewGuid().ToString())]));
        await PortalSecurity.ValidateTenantAsync(context);
        Assert.NotNull(context.Result?.Failure);
    }

    private static RefreshTokenService CreateRefreshTokens(TokenService tokens, IAdministratorRepository administrators) =>
        new(new RefreshTokens(), tokens, new PortalUsers(), administrators,
            Options.Create(new JwtOptions { Key = "local-test-key-at-least-32-characters", Issuer = "test", Audience = "test" }));

    private sealed class RefreshTokens : IRefreshTokenRepository
    {
        public Task CreateAsync(RefreshTokenSession session, byte[] tokenHash, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<RefreshTokenSession?> RotateAsync(byte[] currentTokenHash, byte[] replacementTokenHash, Guid replacementId, DateTimeOffset replacementExpiresAt, CancellationToken cancellationToken = default) => Task.FromResult<RefreshTokenSession?>(null);
    }
    private sealed class PortalUsers : IPortalUserRepository
    {
        public Task<PortalUserAccount?> GetActiveByUsernameAsync(string username, string context, string? tenantCode, CancellationToken cancellationToken = default) => Task.FromResult<PortalUserAccount?>(null);
        public Task<PortalUserAccount?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PortalUserAccount?>(null);
    }

    private sealed class Administrators : IAdministratorRepository
    {
        public AdministratorAccount? Account { get; set; }
        public AdministratorStateResult StateResult { get; set; } = AdministratorStateResult.Updated;
        public Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(Account?.Username == username ? Account : null);
        public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Account?.Id == id && Account.IsActive);
        public Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdministratorSummary>>(Account is null ? [] : [new(Account.Id, Account.Username, Account.Email, Account.IsActive, DateTimeOffset.UtcNow)]);
        public Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default) { Account = account; return Task.CompletedTask; }
        public Task<AdministratorStateResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
        { if (StateResult != AdministratorStateResult.Updated) return Task.FromResult(StateResult); if (Account?.Id != id) return Task.FromResult(AdministratorStateResult.NotFound); Account = Account with { IsActive = isActive }; return Task.FromResult(AdministratorStateResult.Updated); }
        public Task<bool> ResetPasswordAsync(Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default)
        { if (Account?.Id != id) return Task.FromResult(false); Account = Account with { PasswordHash = hash, PasswordSalt = salt, PasswordIterations = iterations }; return Task.FromResult(true); }
    }
}
