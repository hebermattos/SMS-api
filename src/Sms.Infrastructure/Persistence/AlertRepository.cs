using Dapper;
using Sms.Application.Alerts;

namespace Sms.Infrastructure.Persistence;

public sealed class AlertRepository(SqlConnectionFactory connectionFactory) : IAlertRepository
{
    public async Task<IReadOnlyList<AlertRule>> ListRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TenantId, Name, Provider, Status, Threshold, WindowMinutes, RepeatMode,
                   RepeatIntervalMinutes, IsActive, IsTriggered, LastTriggeredAt, CreatedAt, UpdatedAt
            FROM dbo.AlertRules WHERE TenantId=@TenantId ORDER BY CreatedAt DESC;
            """;
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<AlertRule>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task CreateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT dbo.AlertRules
                (Id,TenantId,Name,Provider,Status,Threshold,WindowMinutes,RepeatMode,RepeatIntervalMinutes,IsActive,IsTriggered,CreatedAt)
            VALUES
                (@Id,@TenantId,@Name,@Provider,@Status,@Threshold,@WindowMinutes,@RepeatMode,@RepeatIntervalMinutes,@IsActive,0,@CreatedAt);
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, rule, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE dbo.AlertRules SET Name=@Name,Provider=@Provider,Status=@Status,Threshold=@Threshold,
                WindowMinutes=@WindowMinutes,RepeatMode=@RepeatMode,RepeatIntervalMinutes=@RepeatIntervalMinutes,
                IsActive=@IsActive,IsTriggered=0,LastTriggeredAt=NULL,UpdatedAt=SYSUTCDATETIME()
            WHERE TenantId=@TenantId AND Id=@Id;
            """;
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, rule, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> DeleteRuleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE dbo.AlertRules WHERE TenantId=@TenantId AND Id=@Id;";
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<IReadOnlyList<AlertNotification>> ListAlertsAsync(Guid tenantId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, RuleId, RuleName, Provider, Status, MatchCount, WindowMinutes, CreatedAt, IsRead, ReadAt
            FROM dbo.Alerts
            WHERE TenantId=@TenantId AND (@UnreadOnly=0 OR IsRead=0)
            ORDER BY CreatedAt DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
            """;
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<AlertNotification>(new CommandDefinition(sql,
            new { TenantId = tenantId, UnreadOnly = unreadOnly, Skip = skip, Take = take }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<bool> MarkReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.Alerts SET IsRead=1,ReadAt=SYSUTCDATETIME() WHERE TenantId=@TenantId AND Id=@Id;";
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task MarkAllReadAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE dbo.Alerts SET IsRead=1,ReadAt=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IsRead=0;";
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task EvaluateAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;
            DECLARE @Now DATETIMEOFFSET = SYSUTCDATETIME();
            DECLARE @Evaluation TABLE (RuleId UNIQUEIDENTIFIER PRIMARY KEY, MatchCount INT NOT NULL, ShouldFire BIT NOT NULL);
            DECLARE @Fired TABLE (RuleId UNIQUEIDENTIFIER PRIMARY KEY);

            INSERT @Evaluation(RuleId, MatchCount, ShouldFire)
            SELECT r.Id, counts.MatchCount,
                CASE WHEN counts.MatchCount >= r.Threshold
                    AND (r.IsTriggered=0 OR (r.RepeatMode=2 AND
                         (r.LastTriggeredAt IS NULL OR r.LastTriggeredAt <= DATEADD(MINUTE,-r.RepeatIntervalMinutes,@Now))))
                    THEN 1 ELSE 0 END
            FROM dbo.AlertRules r WITH (UPDLOCK, HOLDLOCK)
            CROSS APPLY (
                SELECT COUNT(DISTINCT h.MessageId) AS MatchCount
                FROM dbo.SmsMessageStatusHistory h
                INNER JOIN dbo.SmsMessages m ON m.TenantId=h.TenantId AND m.Id=h.MessageId
                WHERE h.TenantId=r.TenantId AND h.Status=r.Status
                  AND h.CreatedAt>=DATEADD(MINUTE,-r.WindowMinutes,@Now)
                  AND (r.Provider IS NULL OR m.Provider=r.Provider)
            ) counts
            WHERE r.IsActive=1;

            INSERT dbo.Alerts(Id,TenantId,RuleId,RuleName,Provider,Status,MatchCount,WindowMinutes,CreatedAt,IsRead)
            OUTPUT INSERTED.RuleId INTO @Fired(RuleId)
            SELECT NEWID(),r.TenantId,r.Id,r.Name,r.Provider,r.Status,e.MatchCount,r.WindowMinutes,@Now,0
            FROM dbo.AlertRules r INNER JOIN @Evaluation e ON e.RuleId=r.Id
            WHERE e.ShouldFire=1;

            UPDATE r SET
                IsTriggered=CASE WHEN e.MatchCount>=r.Threshold THEN 1 ELSE 0 END,
                LastTriggeredAt=CASE WHEN f.RuleId IS NOT NULL THEN @Now ELSE r.LastTriggeredAt END,
                UpdatedAt=CASE WHEN f.RuleId IS NOT NULL OR (r.IsTriggered=1 AND e.MatchCount<r.Threshold) THEN @Now ELSE r.UpdatedAt END
            FROM dbo.AlertRules r
            INNER JOIN @Evaluation e ON e.RuleId=r.Id
            LEFT JOIN @Fired f ON f.RuleId=r.Id;

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
