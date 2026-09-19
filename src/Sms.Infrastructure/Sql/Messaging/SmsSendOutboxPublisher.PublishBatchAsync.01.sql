WITH pending AS
            (
                SELECT TOP (100) *
                FROM dbo.SmsSendOutbox WITH (READPAST, UPDLOCK, ROWLOCK)
                WHERE PublishedAtUtc IS NULL
                  AND (LockedUntilUtc IS NULL OR LockedUntilUtc < SYSUTCDATETIME())
                ORDER BY CreatedAtUtc, Id
            )
            UPDATE pending
            SET LockId=@LockId, LockedUntilUtc=DATEADD(MINUTE,1,SYSUTCDATETIME()),
                AttemptCount=AttemptCount+1, LastAttemptAtUtc=SYSUTCDATETIME()
            OUTPUT INSERTED.Id AS EventId, INSERTED.TenantId, INSERTED.MessageId;
