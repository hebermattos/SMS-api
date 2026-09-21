using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantRateLimitRepository(
    SqlConnectionFactory connections,
    IDistributedCache cache,
    ILogger<TenantRateLimitRepository> logger) : ITenantRateLimitRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TenantRateLimitSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(tenantId);
        var cached = await GetCachedAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var value = JsonSerializer.Deserialize<TenantRateLimitSettings>(cached, JsonOptions);
            if (value is not null) return value;
        }

        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantRateLimitRepository.GetAsync.01.sql");
        using var connection = connections.CreateConnection();
        var settings = await connection.QuerySingleOrDefaultAsync<TenantRateLimitSettings>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))
            ?? new TenantRateLimitSettings(120, 10, 6);

        await SetCachedAsync(key, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
        return settings;
    }

    public async Task SaveAsync(Guid tenantId, TenantRateLimitSettings settings, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantRateLimitRepository.SaveAsync.01.sql");
        using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            settings.RequestsPerMinute,
            settings.SmsPerMinute,
            settings.OllamaRequestsPerMinute
        }, cancellationToken: cancellationToken));

        await RemoveCachedAsync(CacheKey(tenantId));
    }

    private async Task<string?> GetCachedAsync(string key, CancellationToken cancellationToken)
    {
        try { return await cache.GetStringAsync(key, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to read tenant settings from distributed cache.");
            return null;
        }
    }

    private async Task SetCachedAsync(string key, string value, CancellationToken cancellationToken)
    {
        try { await cache.SetStringAsync(key, value, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogError(exception, "Failed to write tenant settings to distributed cache."); }
    }

    private async Task RemoveCachedAsync(string key)
    {
        try { await cache.RemoveAsync(key, CancellationToken.None); }
        catch (Exception exception) { logger.LogError(exception, "Failed to invalidate tenant settings in distributed cache."); }
    }

    private static string CacheKey(Guid tenantId) => $"tenant-config:rate-limit:{tenantId:N}";
}
