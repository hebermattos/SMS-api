INSERT INTO TenantSmsOverview
    (TenantId, Outbound, Inbound, Delivered, Failed, Pending, UpdatedAtUtc)
VALUES
    (@TenantId, @OutboundDelta, @InboundDelta, @DeliveredDelta, @FailedDelta, @PendingDelta, @OccurredAtUtc)
ON CONFLICT (TenantId)
DO UPDATE SET
    Outbound=TenantSmsOverview.Outbound + EXCLUDED.Outbound,
    Inbound=TenantSmsOverview.Inbound + EXCLUDED.Inbound,
    Delivered=TenantSmsOverview.Delivered + EXCLUDED.Delivered,
    Failed=TenantSmsOverview.Failed + EXCLUDED.Failed,
    Pending=TenantSmsOverview.Pending + EXCLUDED.Pending,
    UpdatedAtUtc=GREATEST(TenantSmsOverview.UpdatedAtUtc, EXCLUDED.UpdatedAtUtc);

-- Remove the previous message state from the daily aggregate only when this
-- event is current enough to replace the reporting row.
UPDATE SmsDailyOverview daily
SET TotalMessages=daily.TotalMessages - 1,
    UpdatedAtUtc=@OccurredAtUtc
FROM ReportingSmsMessages message
WHERE message.MessageId=@MessageId
  AND @OccurredAtUtc >= message.UpdatedAtUtc
  AND daily.ReportDate=CAST(message.CreatedAtUtc AT TIME ZONE 'UTC' AS date)
  AND daily.TenantId=message.TenantId
  AND daily.Provider=message.Provider
  AND daily.Direction=message.Direction
  AND daily.QueueStatus=message.QueueStatus
  AND daily.Status=message.Status;

DELETE FROM SmsDailyOverview WHERE TotalMessages=0;

INSERT INTO ReportingSmsMessages
    (MessageId, TenantId, TenantName, UserId, Username, Provider, Direction, QueueStatus, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@MessageId, @TenantId, @TenantName, @UserId, @Username, @Provider, @Direction, @QueueStatus, @Status, @CreatedAtUtc, @OccurredAtUtc)
ON CONFLICT (MessageId)
DO UPDATE SET
    TenantName=EXCLUDED.TenantName,
    UserId=EXCLUDED.UserId,
    Username=EXCLUDED.Username,
    Provider=EXCLUDED.Provider,
    Direction=EXCLUDED.Direction,
    QueueStatus=EXCLUDED.QueueStatus,
    Status=EXCLUDED.Status,
    UpdatedAtUtc=EXCLUDED.UpdatedAtUtc
WHERE EXCLUDED.UpdatedAtUtc >= ReportingSmsMessages.UpdatedAtUtc;

-- Add the current message state. Out-of-order events do not match the row
-- after the guarded upsert and therefore cannot corrupt the aggregate.
INSERT INTO SmsDailyOverview
    (ReportDate, TenantId, TenantName, Provider, Direction, QueueStatus, Status, TotalMessages, UpdatedAtUtc)
SELECT
    CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date),
    TenantId,
    TenantName,
    Provider,
    Direction,
    QueueStatus,
    Status,
    1,
    @OccurredAtUtc
FROM ReportingSmsMessages
WHERE MessageId=@MessageId
  AND UpdatedAtUtc=@OccurredAtUtc
  AND TenantId=@TenantId
  AND Provider=@Provider
  AND Direction=@Direction
  AND QueueStatus=@QueueStatus
  AND Status=@Status
ON CONFLICT (ReportDate, TenantId, Provider, Direction, QueueStatus, Status)
DO UPDATE SET
    TenantName=EXCLUDED.TenantName,
    TotalMessages=SmsDailyOverview.TotalMessages + 1,
    UpdatedAtUtc=GREATEST(SmsDailyOverview.UpdatedAtUtc, EXCLUDED.UpdatedAtUtc);

INSERT INTO UserSmsOverview
    (TenantId, UserId, Username, ReportDate, TotalMessages, Delivered, Failed, Pending, UpdatedAtUtc)
SELECT
    @TenantId,
    @UserId,
    COALESCE(@Username, ''),
    CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date),
    CASE WHEN @OutboundDelta > 0 THEN @OutboundDelta ELSE 0 END,
    @DeliveredDelta,
    @FailedDelta,
    @PendingDelta,
    @OccurredAtUtc
WHERE @UserId IS NOT NULL
ON CONFLICT (TenantId, UserId, ReportDate)
DO UPDATE SET
    Username=EXCLUDED.Username,
    TotalMessages=UserSmsOverview.TotalMessages + EXCLUDED.TotalMessages,
    Delivered=UserSmsOverview.Delivered + EXCLUDED.Delivered,
    Failed=UserSmsOverview.Failed + EXCLUDED.Failed,
    Pending=UserSmsOverview.Pending + EXCLUDED.Pending,
    UpdatedAtUtc=GREATEST(UserSmsOverview.UpdatedAtUtc, EXCLUDED.UpdatedAtUtc);
