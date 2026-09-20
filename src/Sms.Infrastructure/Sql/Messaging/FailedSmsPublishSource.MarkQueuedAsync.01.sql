UPDATE SmsMessages
SET QueueStatus = @Queued,
    UpdatedAt = @UpdatedAt
WHERE TenantId = @TenantId
  AND Id = @MessageId
  AND QueueStatus = @NotQueued;
