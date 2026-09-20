WITH inserted_message AS
(
    INSERT INTO SmsMessages
        (Id, TenantId, "From", "To", Body, Provider, ProviderMessageId, Direction, QueueStatus, Status, CreatedAt, ScheduledAtUtc, UpdatedAt)
    VALUES
        (@Id, @TenantId, @From, @To, @Body, @Provider, @ProviderMessageId, @Direction, @QueueStatus, @Status, @CreatedAt, @ScheduledAtUtc, @UpdatedAt)
    ON CONFLICT (TenantId, Provider, ProviderMessageId)
        WHERE ProviderMessageId IS NOT NULL
    DO NOTHING
    RETURNING TenantId, Id, Provider, Status, CreatedAt
),
inserted_history AS
(
    INSERT INTO SmsMessageStatusHistory(Id, TenantId, MessageId, Status, CreatedAt)
    SELECT gen_random_uuid(), TenantId, Id, Status, CreatedAt
    FROM inserted_message
    RETURNING MessageId
)
INSERT INTO AlertStatusCounters
    (TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
SELECT
    TenantId,
    Provider,
    Status,
    date_trunc('minute', CreatedAt),
    1,
    CreatedAt
FROM inserted_message
WHERE Status BETWEEN 1 AND 5
ON CONFLICT (TenantId, Provider, Status, BucketStartUtc)
DO UPDATE SET
    MessageCount=AlertStatusCounters.MessageCount + EXCLUDED.MessageCount,
    UpdatedAtUtc=EXCLUDED.UpdatedAtUtc;
