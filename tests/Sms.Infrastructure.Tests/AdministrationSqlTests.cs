using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Sms.Application.Administration;
using Sms.Application.Auth;
using Sms.Application.Providers;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Providers;
using Sms.Infrastructure.Security;

namespace Sms.Infrastructure.Tests;

public sealed class AdministrationSqlTests
{
    [SqlServerFact]
    public async Task Administration_PreservesIsolationSecretsAndSingleDefault()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] = Environment.GetEnvironmentVariable("SMS_TEST_SQLSERVER"),
            ["Encryption:MasterKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        var factory = new SqlConnectionFactory(configuration);
        var protector = new AesGcmSecretProtector(configuration);
        var repository = new AdministrationRepository(factory, protector);
        var providers = new TenantSmsProviderRepository(factory, protector);
        var service = new AdministrationService(repository, providers, [new TwilioSettingsPolicy(), new BandwidthSettingsPolicy()]);
        var credentials = new ApiClientRepository(factory);
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid(); var account = Guid.NewGuid().ToString("N");
        using var connection = new SqlConnection(configuration.GetConnectionString("SqlServer"));
        await connection.OpenAsync();
        try
        {
            await connection.ExecuteAsync("""
                INSERT dbo.Tenants(Id, Name, IsActive, CreatedAt)
                VALUES (@Tenant, N'Portal test', 1, SYSDATETIMEOFFSET()), (@Other, N'Other portal test', 1, SYSDATETIMEOFFSET());
                """, new { Tenant = tenant, Other = other });
            var issued = await service.CreateClientAsync(tenant, null, default);
            var stored = await credentials.GetActiveByClientIdAsync(issued.ClientId);
            Assert.NotNull(stored); Assert.True(ClientSecretHasher.Verify(issued.ClientSecret, stored.SecretHash, stored.SecretSalt, stored.SecretIterations));
            var client = Assert.Single(await service.ListClientsAsync(tenant, 0, 20, default));
            Assert.Empty(await service.ListClientsAsync(other, 0, 20, default));
            Assert.False(await repository.SetClientActiveAsync(other, client.Id, false, default));
            Assert.Null(await repository.RotateClientSecretAsync(other, client.Id, new byte[32], new byte[32], 100000, default));
            var rotated = await service.RotateClientSecretAsync(tenant, client.Id, default);
            stored = await credentials.GetActiveByClientIdAsync(issued.ClientId);
            Assert.False(ClientSecretHasher.Verify(issued.ClientSecret, stored!.SecretHash, stored.SecretSalt, stored.SecretIterations));
            Assert.True(ClientSecretHasher.Verify(rotated.ClientSecret, stored.SecretHash, stored.SecretSalt, stored.SecretIterations));
            await Assert.ThrowsAsync<AdministrationConflictException>(() => service.CreateClientAsync(other, issued.ClientId, default));
            await service.SetClientActiveAsync(tenant, client.Id, false, default);
            Assert.Null(await credentials.GetActiveByClientIdAsync(issued.ClientId));
            await service.SetClientActiveAsync(tenant, client.Id, true, default);
            await service.UpdateTenantAsync(tenant, "Renamed", false, default);
            Assert.Null(await credentials.GetActiveByClientIdAsync(issued.ClientId));
            await service.UpdateTenantAsync(tenant, "Renamed", true, default);
            Assert.NotNull(await credentials.GetActiveByClientIdAsync(issued.ClientId));

            var twilio = new ProviderEdit(account, "+15550000001", true, true, "local-test-secret", null);
            var bandwidth = new ProviderEdit(account, "+15550000001", true, true, "local-bandwidth-secret",
                new() { ["accountId"] = "messaging", ["applicationId"] = "application", ["webhookPassword"] = "local-callback-password" });
            await service.SaveProviderAsync(tenant, "Twilio", twilio, default);
            await service.SaveProviderAsync(tenant, "Bandwidth", bandwidth, default);
            Assert.Equal("Bandwidth", (await providers.GetDefaultAsync(tenant))!.Provider);
            Assert.False((await providers.GetAsync(tenant, "Twilio"))!.IsDefault);
            Assert.Empty(await repository.ListProvidersAsync(other, default));
            var persisted = await connection.QuerySingleAsync<(string ApiSecret, string Settings)>(
                "SELECT ApiSecret, Settings FROM dbo.TenantSmsProviders WHERE TenantId=@Tenant AND Provider='Bandwidth';", new { Tenant = tenant });
            Assert.DoesNotContain("local-bandwidth-secret", persisted.ApiSecret);
            Assert.DoesNotContain("local-callback-password", persisted.Settings);
            await service.SaveProviderAsync(tenant, "Bandwidth", bandwidth with { ApiSecret = null, Settings = new() { ["webhookPassword"] = "" } }, default);
            Assert.Equal("local-bandwidth-secret", (await providers.GetAsync(tenant, "Bandwidth"))!.ApiSecret);
            await service.SaveProviderAsync(other, "Twilio", twilio with { FromNumber = "+15550000002" }, default);
            await Task.WhenAll(service.SaveProviderAsync(tenant, "Twilio", twilio, default), service.SaveProviderAsync(tenant, "Bandwidth", bandwidth, default));
            Assert.Single(await service.ListProvidersAsync(tenant, default), x => x.IsDefault);
            Assert.Equal("Twilio", (await providers.GetDefaultAsync(other))!.Provider);

            var overview = await new TenantPortalRepository(factory).GetOverviewAsync(tenant, default);
            Assert.Equal("Renamed", overview!.Name); Assert.Equal(0, overview.Outbound); Assert.Equal(2, overview.Providers.Count);
            Assert.Null(await new TenantPortalRepository(factory).GetOverviewAsync(Guid.NewGuid(), default));
        }
        finally
        {
            await connection.ExecuteAsync("""
                DELETE dbo.TenantSmsProviders WHERE TenantId IN (@Tenant, @Other);
                DELETE dbo.ApiClients WHERE TenantId IN (@Tenant, @Other);
                DELETE dbo.Tenants WHERE Id IN (@Tenant, @Other);
                """, new { Tenant = tenant, Other = other });
        }
    }
}
