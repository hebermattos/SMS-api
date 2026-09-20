using Dapper;
using Sms.Application.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantAiSettingsRepository(SqlConnectionFactory connectionFactory) : ITenantAiSettingsRepository
{
    public const string DefaultImprovePrompt = "Improve this SMS. Keep the meaning, make it concise and professional, preserve every {{variableName}} exactly, do not add facts. Return only the improved SMS.";
    public const string DefaultValidatePrompt = "Validate this SMS for clarity, spelling, ambiguous wording and broken {{variableName}} placeholders. Do not judge legal compliance. Return JSON only: {\"isValid\":true,\"issues\":[\"...\"]}.";

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
