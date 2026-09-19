UPDATE dbo.SmsSendOutbox
                SET PublishedAtUtc=SYSUTCDATETIME(), LockId=NULL, LockedUntilUtc=NULL
                WHERE Id=@EventId AND LockId=@LockId AND PublishedAtUtc IS NULL;
