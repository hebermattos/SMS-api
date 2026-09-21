using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Caching;

public sealed class ProviderCatalogCache(
    IDistributedCache cache,
    IEnumerable<IProviderSettingsPolicy> policies,
    ILogger<ProviderCatalogCache> logger) : IProviderCatalogCache
{
    private const string CacheKey = "provider-catalog";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProviderDefinition>> GetAsync(CancellationToken cancellationToken = default)
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
            logger.LogError(exception, "Unable to read {CacheArea} cache.", "ProviderCatalog");
        }

        var catalog = policies.Select(policy => policy.Definition).ToArray();

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
            logger.LogError(exception, "Unable to write {CacheArea} cache.", "ProviderCatalog");
        }

        return catalog;
    }
}
