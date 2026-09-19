WITH candidate AS
(
    SELECT Id, Provider, Status AS PreviousStatus
    FROM SmsMessages
    WHERE TenantId=@TenantId
      AND Id=@Id
      AND ScheduledAtUtc IS NOT NULL
      AND
      (
          (Status=@Scheduled AND ScheduledAtUtc <= @UpdatedAt)
          OR
          (Status=@Queued AND UpdatedAt <= @UpdatedAt - INTERVAL '5 minutes')
      )
    FOR UPDATE
),
changed AS
(
    UPDATE SmsMessages m
    SET Status=@Queued, UpdatedAt=@UpdatedAt
    FROM candidate c
    WHERE m.TenantId=@TenantId AND m.Id=c.Id
    RETURNING m.Id, m.Provider, c.PreviousStatus
),
inserted_history AS
(
    INSERT INTO SmsMessageStatusHistory(Id, TenantId, MessageId, Status, CreatedAt)
    SELECT gen_random_uuid(), @TenantId, Id, @Queued, @UpdatedAt
    FROM changed
    WHERE PreviousStatus<>@Queued
    RETURNING MessageId
),
updated_counter AS
(
    INSERT INTO AlertStatusCounters
        (TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
    SELECT
        @TenantId,
        Provider,
        @Queued,
        date_trunc('minute', @UpdatedAt),
        COUNT(*)::INTEGER,
        @UpdatedAt
    FROM changed
    WHERE PreviousStatus<>@Queued
    GROUP BY Provider
    ON CONFLICT (TenantId, Provider, Status, BucketStartUtc)
    DO UPDATE SET
        MessageCount=AlertStatusCounters.MessageCount + EXCLUDED.MessageCount,
        UpdatedAtUtc=EXCLUDED.UpdatedAtUtc
    RETURNING 1
)
SELECT CASE WHEN EXISTS(SELECT 1 FROM changed) THEN 1 ELSE 0 END;
