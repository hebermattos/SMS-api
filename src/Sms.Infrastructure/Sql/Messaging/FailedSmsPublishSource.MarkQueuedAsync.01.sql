UPDATE SmsMessages
SET Status = @Queued,
    UpdatedAt = @UpdatedAt
WHERE TenantId = @TenantId
  AND Id = @MessageId
  AND Status = @NotQueued;
