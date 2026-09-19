using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application;
using Sms.Application.Alerts;
using Sms.Application.Messages;
using Sms.Application.Providers;
using Sms.Application.Tenants;
using Sms.Domain.Tenants;
using Sms.Infrastructure;
using Sms.Infrastructure.Providers;
using Sms.Infrastructure.Caching;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests;

public sealed class RegistrationAndModelTests
{
    [Fact]
    public void AddApplication_RegistersApplicationServices()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, x => x.ServiceType == typeof(SendSmsService) && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x => x.ServiceType == typeof(ReceiveSmsWebhookService) && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x => x.ServiceType == typeof(TenantProvisioningService) && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x => x.ServiceType == typeof(AlertService) && x.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddInfrastructure_RegistersProvidersAndRepositories()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=sms_api;Username=sms;Password=Password1!",
            ["ConnectionStrings:ReportingPostgres"] = "Host=localhost;Database=sms_api_reporting;Username=sms;Password=Password1!",
            ["ConnectionStrings:Redis"] = "localhost:6379",
            ["Encryption:MasterKey"] = Convert.ToBase64String(new byte[32])
        }).Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        Assert.Equal(3, services.Count(x => x.ServiceType == typeof(ISmsProvider)));
        Assert.Contains(services, x => x.ServiceType == typeof(ISmsProvider) && x.ImplementationType == typeof(MockSmsProvider));
        Assert.Contains(services, x => x.ServiceType == typeof(BandwidthSmsProvider));
        Assert.Contains(services, x => x.ServiceType == typeof(BandwidthWebhookParser) && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x => x.ServiceType == typeof(IHttpClientFactory));
        Assert.Contains(services, x => x.ServiceType == typeof(IDistributedCache));
        Assert.Contains(services, x => x.ServiceType == typeof(TenantConfigurationCache) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, x => x.ServiceType == typeof(ISmsProviderResolver));
        Assert.Contains(services, x => x.ServiceType == typeof(ISmsMessageRepository));
        Assert.Contains(services, x => x.ServiceType == typeof(IAlertRepository));
        Assert.Contains(services, x => x.ServiceType == typeof(ITenantSmsProviderRepository));
        Assert.Contains(services, x => x.ServiceType == typeof(ISmsWebhookUrlProvider));
        Assert.Contains(services, x => x.ServiceType == typeof(Sms.Infrastructure.Persistence.ReportingSqlConnectionFactory));
        Assert.Contains(services, x => x.ServiceType == typeof(Sms.Infrastructure.Messaging.ITenantSmsOverviewOutbox) && x.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, x => x.ServiceType == typeof(Sms.Infrastructure.Messaging.ITenantSmsOverviewEventPublisher) && x.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddInfrastructure_DoesNotRequireRedisConnectionWhenCacheIsDisabled()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cache:Enabled"] = "false",
            ["Encryption:MasterKey"] = Convert.ToBase64String(new byte[32])
        }).Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<CacheOptions>().Enabled);
        Assert.NotNull(provider.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public void CacheOptions_DefaultsToEnabled()
    {
        var options = CacheOptions.From(new ConfigurationBuilder().Build());

        Assert.True(options.Enabled);
    }

    [Fact]
    public async Task MockProvider_ReturnsFlowTestResult()
    {
        var provider = new MockSmsProvider();
        var result = await provider.SendAsync("+15550000000", "+15550000001", "test");

        Assert.StartsWith("mock-", result.ProviderMessageId);
        Assert.Contains(result.Status, new[] { "queued", "sent", "delivered", "failed" });
    }

    [Fact]
    public void TenantAndProviderConfiguration_ExposeConfiguredValues()
    {
        var tenantId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var tenant = new Tenant { Id = tenantId, Name = "Tenant", CreatedAt = createdAt };
        var provider = new TenantSmsProviderConfiguration(tenantId, "Twilio", "account", "secret", "+1", true, true);

        Assert.Equal(tenantId, tenant.Id);
        Assert.Equal("Tenant", tenant.Name);
        Assert.True(tenant.IsActive);
        Assert.Equal(createdAt, tenant.CreatedAt);
        Assert.Equal(tenantId, provider.TenantId);
        Assert.Null(provider.Settings);
    }
}
