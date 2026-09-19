INSERT dbo.TenantSmsOverviewInbox(EventId, ProcessedAtUtc)
SELECT @EventId, SYSUTCDATETIME()
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.TenantSmsOverviewInbox WITH (UPDLOCK, HOLDLOCK) WHERE EventId = @EventId
);
