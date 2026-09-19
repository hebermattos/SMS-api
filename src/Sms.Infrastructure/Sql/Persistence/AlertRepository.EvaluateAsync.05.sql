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
                SELECT COALESCE(SUM(c.MessageCount), 0) AS MatchCount
                FROM dbo.AlertStatusCounters c
                WHERE c.TenantId=r.TenantId AND c.Status=r.Status
                  AND c.BucketStartUtc>=DATEADD(MINUTE,-r.WindowMinutes,@Now)
                  AND (r.Provider IS NULL OR c.Provider=r.Provider)
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
