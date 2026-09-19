SELECT TOP (100) Id AS MessageId, TenantId
FROM dbo.SmsMessages WITH (READPAST)
WHERE (Status = 6 AND ScheduledAtUtc <= SYSUTCDATETIME())
   OR (Status = 1 AND ScheduledAtUtc IS NOT NULL AND UpdatedAt <= DATEADD(MINUTE, -5, SYSUTCDATETIME()))
ORDER BY ScheduledAtUtc, Id;
