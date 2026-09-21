using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.Seed;
using Sms.Application.Auth;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var connectionFactory = new SqlConnectionFactory(configuration);
var portalUsers = new PortalUserRepository(connectionFactory);
var platformUsers = new PortalUserManagementRepository(connectionFactory);
var username = configuration["Admin:Username"] ?? throw new InvalidOperationException("Admin:Username is required for local bootstrap.");
var password = configuration["Admin:Password"] ?? throw new InvalidOperationException("Admin:Password is required for local bootstrap.");
var email = configuration["Admin:Email"] ?? throw new InvalidOperationException("Admin:Email is required for local bootstrap.");
if (await portalUsers.GetActiveByUsernameAsync(username.Trim(), "platform", null) is null)
{
    var allowInsecureBootstrapPassword = configuration.GetValue<bool>("Admin:AllowInsecureBootstrapPassword");
    if (password.Length >= 15)
    {
        await new PortalUserManagementService(platformUsers)
            .CreatePlatformUserAsync(username, email, password, "administrator");
    }
    else if (allowInsecureBootstrapPassword)
    {
        var (hash, salt, iterations) = ClientSecretHasher.Hash(password);
        await platformUsers.CreatePlatformUserAsync(new(
            Guid.NewGuid(), null, username.Trim(), email.Trim().ToLowerInvariant(),
            hash, salt, iterations, "platform", "administrator"));
    }
    else
    {
        throw new ArgumentException("Passwords must contain 15–128 characters.");
    }
}
Console.WriteLine("Initial platform administrator is configured. Existing passwords are not overwritten.");
var configurationCache = new TenantConfigurationCache(
    connectionFactory,
    new SeedDistributedCache(),
    NullLogger<TenantConfigurationCache>.Instance);
var providers = new TenantSmsProviderRepository(
    connectionFactory,
    new AesGcmSecretProtector(configuration),
    configurationCache);
await ExampleProviders.SeedAsync(providers, Guid.Parse("11111111-1111-4111-8111-111111111111"));
Console.WriteLine("Example Twilio and Bandwidth providers configured with fictional credentials. Real SMS delivery is unavailable until valid account credentials are configured.");


internal sealed class SeedDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

    public byte[]? Get(string key) => _values.GetValueOrDefault(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        Task.FromResult(Get(key));

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => _values.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        _values[key] = value;

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}
