WITH candidate AS
(
    SELECT Id, Provider, Status AS PreviousStatus
    FROM SmsMessages
    WHERE TenantId=@TenantId
      AND Provider=@Provider
      AND ProviderMessageId=@ProviderMessageId
      AND
      (
          Status=@Pending
          OR (Status=@Sent AND @Status IN (@Delivered, @Failed))
      )
    FOR UPDATE
),
changed AS
(
    UPDATE SmsMessages m
    SET Status=@Status, UpdatedAt=@UpdatedAt
    FROM candidate c
    WHERE m.TenantId=@TenantId AND m.Id=c.Id
    RETURNING m.Id, m.Provider, c.PreviousStatus
),
inserted_history AS
(
    INSERT INTO SmsMessageStatusHistory(Id, TenantId, MessageId, Status, CreatedAt)
    SELECT gen_random_uuid(), @TenantId, Id, @Status, @UpdatedAt
    FROM changed
    WHERE PreviousStatus<>@Status
    RETURNING MessageId
)
INSERT INTO AlertStatusCounters
    (TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
SELECT
    @TenantId,
    Provider,
    @Status,
    date_trunc('minute', @UpdatedAt),
    COUNT(*)::INTEGER,
    @UpdatedAt
FROM changed
WHERE PreviousStatus<>@Status
  AND @Status BETWEEN 1 AND 5
GROUP BY Provider
ON CONFLICT (TenantId, Provider, Status, BucketStartUtc)
DO UPDATE SET
    MessageCount=AlertStatusCounters.MessageCount + EXCLUDED.MessageCount,
    UpdatedAtUtc=EXCLUDED.UpdatedAtUtc;
