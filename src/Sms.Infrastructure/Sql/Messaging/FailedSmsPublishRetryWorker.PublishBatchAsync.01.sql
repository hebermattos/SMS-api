SELECT Id AS MessageId, TenantId
FROM SmsMessages
WHERE Status=@NotQueued
ORDER BY COALESCE(UpdatedAt, CreatedAt), Id
LIMIT 100;
