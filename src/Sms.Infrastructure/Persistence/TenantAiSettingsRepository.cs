using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sms.Application.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantAiSettingsRepository(
    SqlConnectionFactory connectionFactory,
    IDistributedCache cache,
    ILogger<TenantAiSettingsRepository> logger) : ITenantAiSettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public const string DefaultImprovePrompt = "Improve this SMS. Keep the meaning, make it concise and professional, preserve every {{variableName}} exactly, do not add facts. Return only the improved SMS.";
    public const string DefaultValidatePrompt = "Review this SMS or message template and give concise, actionable suggestions to improve clarity, spelling, tone, length, and ambiguous wording. Check broken {{variableName}} placeholders and preserve variables exactly. Do not rewrite the message and do not judge legal compliance. Return JSON only: {\"isValid\":true,\"issues\":[\"...\"]}. Set isValid to false when you have improvement suggestions.";

    public async Task<TenantAiSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(tenantId);
        var cached = await GetCachedAsync(key, tenantId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var value = JsonSerializer.Deserialize<TenantAiSettings>(cached, JsonOptions);
            if (value is not null) return value;
        }

        using var connection = connectionFactory.CreateConnection();
        var settings = await connection.QuerySingleOrDefaultAsync<TenantAiSettings>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantAiSettingsRepository.GetAsync.01.sql"),
            new { TenantId = tenantId }, cancellationToken: cancellationToken))
            ?? new(DefaultImprovePrompt, DefaultValidatePrompt);

        await SetCachedAsync(key, tenantId, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
        return settings;
    }

    public async Task SaveAsync(Guid tenantId, TenantAiSettings settings, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantAiSettingsRepository.SaveAsync.01.sql"),
            new { TenantId = tenantId, settings.ImprovePrompt, settings.ValidatePrompt, UpdatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken));

        await RemoveCachedAsync(CacheKey(tenantId), tenantId);
    }

    private async Task<string?> GetCachedAsync(string key, Guid tenantId, CancellationToken cancellationToken)
    {
        try { return await cache.GetStringAsync(key, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to read {CacheArea} cache for tenant {TenantId}.", "AiSettings", tenantId);
            return null;
        }
    }

    private async Task SetCachedAsync(string key, Guid tenantId, string value, CancellationToken cancellationToken)
    {
        try { await cache.SetStringAsync(key, value, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogError(exception, "Failed to write {CacheArea} cache for tenant {TenantId}.", "AiSettings", tenantId); }
    }

    private async Task RemoveCachedAsync(string key, Guid tenantId)
    {
        try { await cache.RemoveAsync(key, CancellationToken.None); }
        catch (Exception exception) { logger.LogError(exception, "Failed to invalidate {CacheArea} cache for tenant {TenantId}.", "AiSettings", tenantId); }
    }

    private static string CacheKey(Guid tenantId) => $"tenant-config:ai:{tenantId:N}";
}
