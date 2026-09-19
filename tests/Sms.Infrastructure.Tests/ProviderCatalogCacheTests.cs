using Microsoft.Extensions.Logging.Abstractions;
using Sms.Application.Administration;
using Sms.Infrastructure.Caching;
using Sms.Infrastructure.Providers;

namespace Sms.Infrastructure.Tests;

public sealed class ProviderCatalogCacheTests
{
    [Fact]
    public async Task GetAsync_CachesProviderDefinitionsInDistributedCache()
    {
        var distributedCache = new TenantConfigurationCacheTestFactory.TestDistributedCache();
        var policies = new CountingPolicies();
        var cache = new ProviderCatalogCache(
            distributedCache,
            new CacheOptions(true),
            policies,
            NullLogger<ProviderCatalogCache>.Instance);

        var first = await cache.GetAsync();
        var second = await cache.GetAsync();

        Assert.Equal(2, first.Count);
        Assert.Equal(first, second);
        Assert.Equal(1, policies.EnumerationCount);
    }

    [Fact]
    public async Task GetAsync_WhenCacheIsDisabled_ReadsPoliciesEveryTime()
    {
        var policies = new CountingPolicies();
        var cache = new ProviderCatalogCache(
            new TenantConfigurationCacheTestFactory.TestDistributedCache(),
            new CacheOptions(false),
            policies,
            NullLogger<ProviderCatalogCache>.Instance);

        await cache.GetAsync();
        await cache.GetAsync();

        Assert.Equal(2, policies.EnumerationCount);
    }

    private sealed class CountingPolicies : IEnumerable<IProviderSettingsPolicy>
    {
        public int EnumerationCount { get; private set; }

        public IEnumerator<IProviderSettingsPolicy> GetEnumerator()
        {
            EnumerationCount++;
            return new IProviderSettingsPolicy[]
            {
                new TwilioSettingsPolicy(),
                new BandwidthSettingsPolicy()
            }.AsEnumerable().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
