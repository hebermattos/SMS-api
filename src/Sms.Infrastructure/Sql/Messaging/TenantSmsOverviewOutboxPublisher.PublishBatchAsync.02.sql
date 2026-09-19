UPDATE dbo.TenantSmsOverviewOutbox
SET PublishedAtUtc = SYSUTCDATETIME(),
    AttemptCount = AttemptCount + 1,
    LastAttemptAtUtc = SYSUTCDATETIME()
WHERE EventId = @EventId AND PublishedAtUtc IS NULL;
