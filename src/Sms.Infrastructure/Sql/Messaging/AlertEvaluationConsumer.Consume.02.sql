DECLARE @Now DATETIMEOFFSET = @OccurredAtUtc;
            DECLARE @Fired TABLE (RuleId UNIQUEIDENTIFIER PRIMARY KEY);

            INSERT dbo.Alerts(Id,TenantId,RuleId,RuleName,Provider,Status,MatchCount,WindowMinutes,CreatedAt,IsRead)
            OUTPUT INSERTED.RuleId INTO @Fired
            SELECT NEWID(), r.TenantId, r.Id, r.Name, r.Provider, r.Status,
                   counts.MatchCount,
                   r.WindowMinutes, @Now, 0
            FROM dbo.AlertRules r WITH (UPDLOCK, HOLDLOCK)
            CROSS APPLY
            (
                SELECT COALESCE(SUM(c.MessageCount), 0) AS MatchCount
                FROM dbo.AlertStatusCounters c
                WHERE c.TenantId = r.TenantId
                  AND c.Status = r.Status
                  AND c.BucketStartUtc >= DATEADD(MINUTE, -r.WindowMinutes, @Now)
                  AND (r.Provider IS NULL OR c.Provider = r.Provider)
            ) counts
            WHERE r.TenantId=@TenantId AND r.IsActive=1 AND r.Status=@Status
              AND (r.Provider IS NULL OR r.Provider=@Provider)
              AND (r.IsTriggered=0 OR (r.RepeatMode=2 AND
                   (r.LastTriggeredAt IS NULL OR r.LastTriggeredAt<=DATEADD(MINUTE,-r.RepeatIntervalMinutes,@Now))))
              AND counts.MatchCount >= r.Threshold;

            UPDATE r SET IsTriggered=1, LastTriggeredAt=@Now, UpdatedAt=@Now
            FROM dbo.AlertRules r INNER JOIN @Fired f ON f.RuleId=r.Id;
