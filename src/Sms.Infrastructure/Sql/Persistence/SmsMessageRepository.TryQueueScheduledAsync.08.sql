SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Changed TABLE (Id UNIQUEIDENTIFIER, Provider NVARCHAR(50), PreviousStatus INT);

UPDATE dbo.SmsMessages
SET Status = @Queued, UpdatedAt = @UpdatedAt
OUTPUT INSERTED.Id, INSERTED.Provider, DELETED.Status INTO @Changed
WHERE TenantId = @TenantId
  AND Id = @Id
  AND ScheduledAtUtc IS NOT NULL
  AND
  (
      (Status = @Scheduled AND ScheduledAtUtc <= @UpdatedAt)
      OR (Status = @Queued AND UpdatedAt <= DATEADD(MINUTE, -5, @UpdatedAt))
  );

INSERT dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
SELECT NEWID(), @TenantId, Id, @Queued, @UpdatedAt FROM @Changed WHERE PreviousStatus <> @Queued;

DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@UpdatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
UPDATE c WITH (UPDLOCK, SERIALIZABLE)
SET MessageCount = MessageCount + 1, UpdatedAtUtc = @UpdatedAt
FROM dbo.AlertStatusCounters c
INNER JOIN @Changed x ON x.Provider = c.Provider AND x.PreviousStatus <> @Queued
WHERE c.TenantId = @TenantId AND c.Status = @Queued AND c.BucketStartUtc = @BucketStartUtc;

INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
SELECT @TenantId, x.Provider, @Queued, @BucketStartUtc, 1, @UpdatedAt
FROM @Changed x
WHERE x.PreviousStatus <> @Queued
  AND NOT EXISTS
(
    SELECT 1 FROM dbo.AlertStatusCounters c
    WHERE c.TenantId = @TenantId AND c.Provider = x.Provider AND c.Status = @Queued AND c.BucketStartUtc = @BucketStartUtc
);

DECLARE @WasQueued INT = CASE WHEN EXISTS (SELECT 1 FROM @Changed) THEN 1 ELSE 0 END;
COMMIT TRANSACTION;
SELECT @WasQueued;
