INSERT dbo.AlertEvaluationInbox(EventId, ProcessedAtUtc)
            SELECT @EventId, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertEvaluationInbox WITH (UPDLOCK, HOLDLOCK) WHERE EventId=@EventId);
