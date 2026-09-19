using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Caching;

public sealed class ProviderCatalogCache(
    IDistributedCache cache,
    CacheOptions cacheOptions,
    IEnumerable<IProviderSettingsPolicy> policies,
    ILogger<ProviderCatalogCache> logger) : IProviderCatalogCache
{
    private const string CacheKey = "provider-catalog";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProviderDefinition>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cacheOptions.Enabled)
        {
            try
            {
                var cached = await cache.GetStringAsync(CacheKey, cancellationToken);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    var definitions = JsonSerializer.Deserialize<ProviderDefinition[]>(cached, JsonOptions);
                    if (definitions is not null) return definitions;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to read provider catalog cache.");
            }
        }

        var catalog = policies.Select(policy => policy.Definition).ToArray();

        if (cacheOptions.Enabled)
        {
            try
            {
                await cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(catalog, JsonOptions), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unable to write provider catalog cache.");
            }
        }

        return catalog;
    }
}
