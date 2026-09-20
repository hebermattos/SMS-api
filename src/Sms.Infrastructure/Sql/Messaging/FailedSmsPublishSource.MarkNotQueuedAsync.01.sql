UPDATE SmsMessages
SET QueueStatus = @NotQueued,
    UpdatedAt = @UpdatedAt
WHERE TenantId = @TenantId
  AND Id = @MessageId
  AND QueueStatus = @Queued;
