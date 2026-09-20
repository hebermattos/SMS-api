using Microsoft.Extensions.Options;
using Sms.Api.Auth;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Tests;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task Issue_CreatesHashedRotatingSessionAndAccessToken()
    {
        var repository = new RefreshTokens();
        var user = User();
        var service = Service(repository, new Users(user), new Administrators());
        var issued = await service.IssueAsync(user.Id, user.Username, user.TenantId, user.Context, user.Role);

        Assert.False(string.IsNullOrWhiteSpace(issued.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(issued.RefreshToken));
        Assert.Equal(3600, issued.ExpiresIn);
        Assert.NotNull(repository.Created);
        Assert.Equal(user.Id, repository.Created!.UserId);
        Assert.Equal(32, repository.CreatedHash!.Length);
        Assert.DoesNotContain(Convert.ToBase64String(repository.CreatedHash), issued.RefreshToken);
    }

    [Fact]
    public async Task Rotate_RejectsInvalidMissingAndChangedPortalUsers()
    {
        var user = User();
        var repository = new RefreshTokens { Rotated = Session(user) };
        var users = new Users(user);
        var service = Service(repository, users, new Administrators());

        Assert.Null(await service.RotateAsync(""));
        Assert.Null(await service.RotateAsync(new string('x', 513)));

        repository.Rotated = null;
        Assert.Null(await service.RotateAsync("token"));

        repository.Rotated = Session(user);
        users.Account = null;
        Assert.Null(await service.RotateAsync("token"));

        foreach (var changed in new[]
        {
            user with { Context = "platform" },
            user with { Role = "administrator" },
            user with { TenantId = Guid.NewGuid() }
        })
        {
            users.Account = changed;
            Assert.Null(await service.RotateAsync("token"));
        }
    }

    [Fact]
    public async Task Rotate_IssuesReplacementForValidPortalUser()
    {
        var user = User();
        var repository = new RefreshTokens { Rotated = Session(user) };
        var service = Service(repository, new Users(user), new Administrators());
        var rotated = await service.RotateAsync("old-refresh-token");

        Assert.NotNull(rotated);
        Assert.NotEqual("old-refresh-token", rotated!.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(rotated.AccessToken));
        Assert.Equal(32, repository.CurrentHash!.Length);
        Assert.Equal(32, repository.ReplacementHash!.Length);
        Assert.NotEqual(repository.Rotated!.Id, repository.ReplacementId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Rotate_RechecksPlatformAdministrator(bool active, bool succeeds)
    {
        var id = Guid.NewGuid();
        var repository = new RefreshTokens
        {
            Rotated = new RefreshTokenSession(Guid.NewGuid(), id, "admin", null, "platform", "administrator", true, DateTimeOffset.UtcNow.AddDays(1))
        };
        var service = Service(repository, new Users(null), new Administrators { Active = active });
        var result = await service.RotateAsync("admin-token");
        Assert.Equal(succeeds, result is not null);
    }

    [Fact]
    public async Task Issue_CreatesAdministratorTokenWhenRequested()
    {
        var repository = new RefreshTokens();
        var id = Guid.NewGuid();
        var service = Service(repository, new Users(null), new Administrators { Active = true });
        var issued = await service.IssueAsync(id, "admin", null, "platform", "administrator", true);
        Assert.False(string.IsNullOrWhiteSpace(issued.AccessToken));
        Assert.True(repository.Created!.IsPlatformAdministrator);
    }

    private static RefreshTokenService Service(RefreshTokens repository, Users users, Administrators administrators)
    {
        var options = Options.Create(new JwtOptions
        {
            Key = "local-test-key-at-least-32-characters",
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = 60,
            RefreshExpirationDays = 7
        });
        return new RefreshTokenService(repository, new TokenService(options), users, administrators, options);
    }

    private static PortalUserAccount User() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "user", "user@example.com", [], [], 600000, "tenant", "user", true);

    private static RefreshTokenSession Session(PortalUserAccount user) =>
        new(Guid.NewGuid(), user.Id, user.Username, user.TenantId, user.Context, user.Role, false, DateTimeOffset.UtcNow.AddDays(1));

    private sealed class RefreshTokens : IRefreshTokenRepository
    {
        public RefreshTokenSession? Created { get; private set; }
        public byte[]? CreatedHash { get; private set; }
        public RefreshTokenSession? Rotated { get; set; }
        public byte[]? CurrentHash { get; private set; }
        public byte[]? ReplacementHash { get; private set; }
        public Guid ReplacementId { get; private set; }

        public Task CreateAsync(RefreshTokenSession session, byte[] tokenHash, CancellationToken cancellationToken = default)
        { Created = session; CreatedHash = tokenHash; return Task.CompletedTask; }

        public Task<RefreshTokenSession?> RotateAsync(byte[] currentTokenHash, byte[] replacementTokenHash, Guid replacementId,
            DateTimeOffset replacementExpiresAt, CancellationToken cancellationToken = default)
        {
            CurrentHash = currentTokenHash; ReplacementHash = replacementTokenHash; ReplacementId = replacementId;
            return Task.FromResult(Rotated);
        }
    }

    private sealed class Users(PortalUserAccount? account) : IPortalUserRepository
    {
        public PortalUserAccount? Account { get; set; } = account;
        public Task<PortalUserAccount?> GetActiveByUsernameAsync(string username, string context, string? tenantCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Account);
        public Task<PortalUserAccount?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Account?.Id == id ? Account : null);
    }

    private sealed class Administrators : IAdministratorRepository
    {
        public bool Active { get; set; }
        public Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Active);
        public Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<AdministratorAccount?>(null);
        public Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AdministratorSummary>>([]);
        public Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AdministratorStateResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) => Task.FromResult(AdministratorStateResult.Updated);
        public Task<bool> ResetPasswordAsync(Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
