using Dapper;
using MassTransit;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertEvaluationConsumer(SqlConnectionFactory connectionFactory) : IConsumer<AlertEvaluationEvent>
{
    public async Task Consume(ConsumeContext<AlertEvaluationEvent> context)
    {
        var message = context.Message;
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);

        var inserted = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT dbo.AlertEvaluationInbox(EventId, ProcessedAtUtc)
            SELECT @EventId, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertEvaluationInbox WITH (UPDLOCK, HOLDLOCK) WHERE EventId=@EventId);
            """, new { message.EventId }, transaction, cancellationToken: context.CancellationToken));
        if (inserted == 0) { transaction.Commit(); return; }

        const string sql = """
            DECLARE @Now DATETIMEOFFSET = @OccurredAtUtc;
            DECLARE @Fired TABLE (RuleId UNIQUEIDENTIFIER PRIMARY KEY);

            INSERT dbo.Alerts(Id,TenantId,RuleId,RuleName,Provider,Status,MatchCount,WindowMinutes,CreatedAt,IsRead)
            OUTPUT INSERTED.RuleId INTO @Fired
            SELECT NEWID(), r.TenantId, r.Id, r.Name, r.Provider, r.Status,
                   COALESCE((SELECT SUM(c.MessageCount) FROM dbo.AlertStatusCounters c
                             WHERE c.TenantId=r.TenantId AND c.Status=r.Status
                               AND c.BucketStartUtc>=DATEADD(MINUTE,-r.WindowMinutes,@Now)
                               AND (r.Provider IS NULL OR c.Provider=r.Provider)),0),
                   r.WindowMinutes, @Now, 0
            FROM dbo.AlertRules r WITH (UPDLOCK, HOLDLOCK)
            WHERE r.TenantId=@TenantId AND r.IsActive=1 AND r.Status=@Status
              AND (r.Provider IS NULL OR r.Provider=@Provider)
              AND (r.IsTriggered=0 OR (r.RepeatMode=2 AND
                   (r.LastTriggeredAt IS NULL OR r.LastTriggeredAt<=DATEADD(MINUTE,-r.RepeatIntervalMinutes,@Now))))
              AND COALESCE((SELECT SUM(c.MessageCount) FROM dbo.AlertStatusCounters c
                            WHERE c.TenantId=r.TenantId AND c.Status=r.Status
                              AND c.BucketStartUtc>=DATEADD(MINUTE,-r.WindowMinutes,@Now)
                              AND (r.Provider IS NULL OR c.Provider=r.Provider)),0) >= r.Threshold;

            UPDATE r SET IsTriggered=1, LastTriggeredAt=@Now, UpdatedAt=@Now
            FROM dbo.AlertRules r INNER JOIN @Fired f ON f.RuleId=r.Id;
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = message.TenantId, Provider = message.Provider, Status = message.Status,
            OccurredAtUtc = message.OccurredAtUtc
        }, transaction, cancellationToken: context.CancellationToken));
        transaction.Commit();
    }
}
