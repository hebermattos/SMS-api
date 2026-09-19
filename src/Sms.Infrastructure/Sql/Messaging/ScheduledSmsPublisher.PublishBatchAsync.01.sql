SELECT Id AS MessageId, TenantId
FROM SmsMessages
WHERE (Status=6 AND ScheduledAtUtc <= CURRENT_TIMESTAMP)
   OR (Status=1 AND ScheduledAtUtc IS NOT NULL AND UpdatedAt <= CURRENT_TIMESTAMP - INTERVAL '5 minutes')
ORDER BY ScheduledAtUtc, Id
LIMIT 100;
