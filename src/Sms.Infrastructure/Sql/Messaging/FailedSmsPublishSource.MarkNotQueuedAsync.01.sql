UPDATE SmsMessages
SET Status = @NotQueued,
    UpdatedAt = @UpdatedAt
WHERE TenantId = @TenantId
  AND Id = @MessageId
  AND Status = @Queued;
