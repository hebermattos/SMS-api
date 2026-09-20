SELECT Id AS MessageId, TenantId
FROM SmsMessages
WHERE QueueStatus=@NotQueued
ORDER BY COALESCE(UpdatedAt, CreatedAt), Id
LIMIT 100;
