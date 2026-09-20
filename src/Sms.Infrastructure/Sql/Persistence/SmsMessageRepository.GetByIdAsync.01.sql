SELECT Id, TenantId, UserId, "From", "To", Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, ScheduledAtUtc, UpdatedAt
FROM SmsMessages
WHERE TenantId=@TenantId AND Id=@Id;
