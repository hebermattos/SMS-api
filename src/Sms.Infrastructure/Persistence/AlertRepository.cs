using Dapper;
using Sms.Application.Alerts;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class AlertRepository(
    SqlConnectionFactory connectionFactory,
    ReportingSqlConnectionFactory reportingConnectionFactory) : IAlertRepository
{
    public async Task<IReadOnlyList<AlertRule>> ListRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.ListRulesAsync.01.sql");
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<AlertRule>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task CreateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.CreateRuleAsync.02.sql");
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, rule, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.UpdateRuleAsync.03.sql");
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, rule, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> DeleteRuleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.DeleteRuleAsync.06.sql");
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<AlertNotification>> ListAlertsAsync(Guid tenantId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.ListAlertsAsync.04.sql");
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<AlertNotification>(new CommandDefinition(sql,
            new { TenantId = tenantId, UnreadOnly = unreadOnly, Skip = skip, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<bool> MarkReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.MarkReadAsync.07.sql");
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task MarkAllReadAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.MarkAllReadAsync.08.sql");
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task EvaluateAsync(Guid tenantId, SmsStatus status, string provider, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var rules = (await ListRulesAsync(tenantId, cancellationToken))
            .Where(r => r.IsActive && r.Status == status && (r.Provider is null || r.Provider == provider));

        var countSql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.EvaluateAsync.05.sql");
        var fireSql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AlertRepository.FireAsync.09.sql");

        using var reporting = reportingConnectionFactory.CreateConnection();
        using var application = connectionFactory.CreateConnection();

        foreach (var rule in rules)
        {
            var matchCount = await reporting.ExecuteScalarAsync<int>(new CommandDefinition(countSql, new
            {
                TenantId = tenantId,
                Status = status,
                Provider = rule.Provider,
                WindowMinutes = rule.WindowMinutes,
                OccurredAtUtc = occurredAtUtc
            }, cancellationToken: cancellationToken));

            if (matchCount == 0) continue;

            await application.ExecuteAsync(new CommandDefinition(fireSql, new
            {
                TenantId = tenantId,
                RuleId = rule.Id,
                RuleName = rule.Name,
                Provider = rule.Provider,
                Status = rule.Status,
                MatchCount = matchCount,
                WindowMinutes = rule.WindowMinutes,
                OccurredAtUtc = occurredAtUtc
            }, cancellationToken: cancellationToken));
        }
    }
}
