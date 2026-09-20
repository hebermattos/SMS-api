using Dapper;
using Sms.Application.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantAiSettingsRepository(SqlConnectionFactory connectionFactory) : ITenantAiSettingsRepository
{
    public const string DefaultImprovePrompt = "Improve this SMS. Keep the meaning, make it concise and professional, preserve every {{variableName}} exactly, do not add facts. Return only the improved SMS.";
    public const string DefaultValidatePrompt = "Review this SMS or message template and give concise, actionable suggestions to improve clarity, spelling, tone, length, and ambiguous wording. Check broken {{variableName}} placeholders and preserve variables exactly. Do not rewrite the message and do not judge legal compliance. Return JSON only: {\"isValid\":true,\"issues\":[\"...\"]}. Set isValid to false when you have improvement suggestions.";

    public async Task<TenantAiSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TenantAiSettings>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantAiSettingsRepository.GetAsync.01.sql"),
            new { TenantId = tenantId }, cancellationToken: cancellationToken))
            ?? new(DefaultImprovePrompt, DefaultValidatePrompt);
    }

    public async Task SaveAsync(Guid tenantId, TenantAiSettings settings, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantAiSettingsRepository.SaveAsync.01.sql"),
            new { TenantId = tenantId, settings.ImprovePrompt, settings.ValidatePrompt, UpdatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken));
    }
}
