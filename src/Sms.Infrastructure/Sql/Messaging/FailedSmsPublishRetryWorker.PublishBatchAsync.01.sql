SELECT Id AS MessageId, TenantId
FROM SmsMessages
WHERE Status=@PublishFailed
ORDER BY COALESCE(UpdatedAt, CreatedAt), Id
LIMIT 100;
