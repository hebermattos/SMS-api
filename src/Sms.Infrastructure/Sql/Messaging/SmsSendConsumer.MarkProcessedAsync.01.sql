INSERT dbo.SmsSendInbox(EventId, ProcessedAtUtc)
            SELECT @EventId, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM dbo.SmsSendInbox WHERE EventId=@EventId);
