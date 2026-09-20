SELECT Id AS MessageId, TenantId
FROM SmsMessages
WHERE QueueStatus=@NotQueued
  AND (ScheduledAtUtc IS NULL OR ScheduledAtUtc <= CURRENT_TIMESTAMP)
ORDER BY COALESCE(ScheduledAtUtc, UpdatedAt, CreatedAt), Id
LIMIT 100;
