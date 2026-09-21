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

-- Remove the previous current state before replacing it.
UPDATE TenantSmsDailyOverview d SET
    TotalMessages=d.TotalMessages-1,
    Scheduled=d.Scheduled-CASE WHEN m.QueueStatus=3 THEN 1 ELSE 0 END,
    Queued=d.Queued-CASE WHEN m.QueueStatus=2 THEN 1 ELSE 0 END,
    Sent=d.Sent-CASE WHEN m.Status=2 THEN 1 ELSE 0 END,
    Delivered=d.Delivered-CASE WHEN m.Status=3 THEN 1 ELSE 0 END,
    Failed=d.Failed-CASE WHEN m.Status=4 THEN 1 ELSE 0 END,
    Received=d.Received-CASE WHEN m.Status=5 THEN 1 ELSE 0 END,
    Outbound=d.Outbound-CASE WHEN m.Direction=1 THEN 1 ELSE 0 END,
    Inbound=d.Inbound-CASE WHEN m.Direction=2 THEN 1 ELSE 0 END,
    Pending=d.Pending-CASE WHEN m.Status=1 THEN 1 ELSE 0 END,
    UpdatedAtUtc=GREATEST(d.UpdatedAtUtc, @OccurredAtUtc)
FROM ReportingSmsMessages m
WHERE m.MessageId=@MessageId AND @OccurredAtUtc>=m.UpdatedAtUtc
  AND d.ReportDate=CAST(m.CreatedAtUtc AT TIME ZONE 'UTC' AS date) AND d.TenantId=m.TenantId;

UPDATE ProviderSmsDailyOverview d SET
    TotalMessages=d.TotalMessages-1,
    Scheduled=d.Scheduled-CASE WHEN m.QueueStatus=3 THEN 1 ELSE 0 END,
    Queued=d.Queued-CASE WHEN m.QueueStatus=2 THEN 1 ELSE 0 END,
    Sent=d.Sent-CASE WHEN m.Status=2 THEN 1 ELSE 0 END,
    Delivered=d.Delivered-CASE WHEN m.Status=3 THEN 1 ELSE 0 END,
    Failed=d.Failed-CASE WHEN m.Status=4 THEN 1 ELSE 0 END,
    Received=d.Received-CASE WHEN m.Status=5 THEN 1 ELSE 0 END,
    Outbound=d.Outbound-CASE WHEN m.Direction=1 THEN 1 ELSE 0 END,
    Inbound=d.Inbound-CASE WHEN m.Direction=2 THEN 1 ELSE 0 END,
    Pending=d.Pending-CASE WHEN m.Status=1 THEN 1 ELSE 0 END,
    UpdatedAtUtc=GREATEST(d.UpdatedAtUtc, @OccurredAtUtc)
FROM ReportingSmsMessages m
WHERE m.MessageId=@MessageId AND @OccurredAtUtc>=m.UpdatedAtUtc
  AND d.ReportDate=CAST(m.CreatedAtUtc AT TIME ZONE 'UTC' AS date) AND d.TenantId=m.TenantId AND d.Provider=m.Provider;

UPDATE UserSmsOverview d SET
    TotalMessages=d.TotalMessages-1,
    Scheduled=d.Scheduled-CASE WHEN m.QueueStatus=3 THEN 1 ELSE 0 END,
    Queued=d.Queued-CASE WHEN m.QueueStatus=2 THEN 1 ELSE 0 END,
    Sent=d.Sent-CASE WHEN m.Status=2 THEN 1 ELSE 0 END,
    Delivered=d.Delivered-CASE WHEN m.Status=3 THEN 1 ELSE 0 END,
    Failed=d.Failed-CASE WHEN m.Status=4 THEN 1 ELSE 0 END,
    Received=d.Received-CASE WHEN m.Status=5 THEN 1 ELSE 0 END,
    Outbound=d.Outbound-CASE WHEN m.Direction=1 THEN 1 ELSE 0 END,
    Inbound=d.Inbound-CASE WHEN m.Direction=2 THEN 1 ELSE 0 END,
    Pending=d.Pending-CASE WHEN m.Status=1 THEN 1 ELSE 0 END,
    UpdatedAtUtc=GREATEST(d.UpdatedAtUtc, @OccurredAtUtc)
FROM ReportingSmsMessages m
WHERE m.MessageId=@MessageId AND @OccurredAtUtc>=m.UpdatedAtUtc AND m.UserId IS NOT NULL
  AND d.ReportDate=CAST(m.CreatedAtUtc AT TIME ZONE 'UTC' AS date) AND d.TenantId=m.TenantId AND d.UserId=m.UserId;

DELETE FROM TenantSmsDailyOverview WHERE TotalMessages=0;
DELETE FROM ProviderSmsDailyOverview WHERE TotalMessages=0;
DELETE FROM UserSmsOverview WHERE TotalMessages=0;

INSERT INTO ReportingSmsMessages
    (MessageId, TenantId, TenantName, UserId, Username, Provider, Direction, QueueStatus, Status, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@MessageId, @TenantId, @TenantName, @UserId, @Username, @Provider, @Direction, @QueueStatus, @Status, @CreatedAtUtc, @OccurredAtUtc)
ON CONFLICT (MessageId)
DO UPDATE SET
    TenantName=EXCLUDED.TenantName, UserId=EXCLUDED.UserId, Username=EXCLUDED.Username,
    Provider=EXCLUDED.Provider, Direction=EXCLUDED.Direction, QueueStatus=EXCLUDED.QueueStatus,
    Status=EXCLUDED.Status, UpdatedAtUtc=EXCLUDED.UpdatedAtUtc
WHERE EXCLUDED.UpdatedAtUtc >= ReportingSmsMessages.UpdatedAtUtc;

-- Older events are kept for delta counters and idempotency, but must not rewrite current-state aggregates.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM ReportingSmsMessages
        WHERE MessageId=@MessageId AND UpdatedAtUtc=@OccurredAtUtc
          AND TenantId=@TenantId AND Provider=@Provider AND Direction=@Direction
          AND QueueStatus=@QueueStatus AND Status=@Status
    ) THEN
        RETURN;
    END IF;
END $$;

-- Add the new current state to each reporting grain.
INSERT INTO TenantSmsDailyOverview
    (ReportDate,TenantId,TenantName,TotalMessages,Scheduled,Queued,Sent,Delivered,Failed,Received,Outbound,Inbound,Pending,UpdatedAtUtc)
SELECT CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date),TenantId,TenantName,1,
    CASE WHEN QueueStatus=3 THEN 1 ELSE 0 END,CASE WHEN QueueStatus=2 THEN 1 ELSE 0 END,
    CASE WHEN Status=2 THEN 1 ELSE 0 END,CASE WHEN Status=3 THEN 1 ELSE 0 END,
    CASE WHEN Status=4 THEN 1 ELSE 0 END,CASE WHEN Status=5 THEN 1 ELSE 0 END,
    CASE WHEN Direction=1 THEN 1 ELSE 0 END,CASE WHEN Direction=2 THEN 1 ELSE 0 END,
    CASE WHEN Status=1 THEN 1 ELSE 0 END,UpdatedAtUtc
FROM ReportingSmsMessages
WHERE MessageId=@MessageId AND UpdatedAtUtc>=@OccurredAtUtc
  AND TenantId=@TenantId AND Provider=@Provider AND Direction=@Direction AND QueueStatus=@QueueStatus AND Status=@Status
ON CONFLICT (ReportDate,TenantId) DO UPDATE SET
    TenantName=EXCLUDED.TenantName,TotalMessages=TenantSmsDailyOverview.TotalMessages+1,
    Scheduled=TenantSmsDailyOverview.Scheduled+EXCLUDED.Scheduled,Queued=TenantSmsDailyOverview.Queued+EXCLUDED.Queued,
    Sent=TenantSmsDailyOverview.Sent+EXCLUDED.Sent,Delivered=TenantSmsDailyOverview.Delivered+EXCLUDED.Delivered,
    Failed=TenantSmsDailyOverview.Failed+EXCLUDED.Failed,Received=TenantSmsDailyOverview.Received+EXCLUDED.Received,
    Outbound=TenantSmsDailyOverview.Outbound+EXCLUDED.Outbound,Inbound=TenantSmsDailyOverview.Inbound+EXCLUDED.Inbound,
    Pending=TenantSmsDailyOverview.Pending+EXCLUDED.Pending,UpdatedAtUtc=GREATEST(TenantSmsDailyOverview.UpdatedAtUtc,EXCLUDED.UpdatedAtUtc);

INSERT INTO ProviderSmsDailyOverview
    (ReportDate,TenantId,TenantName,Provider,TotalMessages,Scheduled,Queued,Sent,Delivered,Failed,Received,Outbound,Inbound,Pending,UpdatedAtUtc)
SELECT CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date),TenantId,TenantName,Provider,1,
    CASE WHEN QueueStatus=3 THEN 1 ELSE 0 END,CASE WHEN QueueStatus=2 THEN 1 ELSE 0 END,
    CASE WHEN Status=2 THEN 1 ELSE 0 END,CASE WHEN Status=3 THEN 1 ELSE 0 END,
    CASE WHEN Status=4 THEN 1 ELSE 0 END,CASE WHEN Status=5 THEN 1 ELSE 0 END,
    CASE WHEN Direction=1 THEN 1 ELSE 0 END,CASE WHEN Direction=2 THEN 1 ELSE 0 END,
    CASE WHEN Status=1 THEN 1 ELSE 0 END,UpdatedAtUtc
FROM ReportingSmsMessages
WHERE MessageId=@MessageId AND UpdatedAtUtc>=@OccurredAtUtc
  AND TenantId=@TenantId AND Provider=@Provider AND Direction=@Direction AND QueueStatus=@QueueStatus AND Status=@Status
ON CONFLICT (ReportDate,TenantId,Provider) DO UPDATE SET
    TenantName=EXCLUDED.TenantName,TotalMessages=ProviderSmsDailyOverview.TotalMessages+1,
    Scheduled=ProviderSmsDailyOverview.Scheduled+EXCLUDED.Scheduled,Queued=ProviderSmsDailyOverview.Queued+EXCLUDED.Queued,
    Sent=ProviderSmsDailyOverview.Sent+EXCLUDED.Sent,Delivered=ProviderSmsDailyOverview.Delivered+EXCLUDED.Delivered,
    Failed=ProviderSmsDailyOverview.Failed+EXCLUDED.Failed,Received=ProviderSmsDailyOverview.Received+EXCLUDED.Received,
    Outbound=ProviderSmsDailyOverview.Outbound+EXCLUDED.Outbound,Inbound=ProviderSmsDailyOverview.Inbound+EXCLUDED.Inbound,
    Pending=ProviderSmsDailyOverview.Pending+EXCLUDED.Pending,UpdatedAtUtc=GREATEST(ProviderSmsDailyOverview.UpdatedAtUtc,EXCLUDED.UpdatedAtUtc);

INSERT INTO UserSmsOverview
    (TenantId,UserId,Username,ReportDate,TotalMessages,Scheduled,Queued,Sent,Delivered,Failed,Received,Outbound,Inbound,Pending,UpdatedAtUtc)
SELECT TenantId,UserId,COALESCE(Username,''),CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date),1,
    CASE WHEN QueueStatus=3 THEN 1 ELSE 0 END,CASE WHEN QueueStatus=2 THEN 1 ELSE 0 END,
    CASE WHEN Status=2 THEN 1 ELSE 0 END,CASE WHEN Status=3 THEN 1 ELSE 0 END,
    CASE WHEN Status=4 THEN 1 ELSE 0 END,CASE WHEN Status=5 THEN 1 ELSE 0 END,
    CASE WHEN Direction=1 THEN 1 ELSE 0 END,CASE WHEN Direction=2 THEN 1 ELSE 0 END,
    CASE WHEN Status=1 THEN 1 ELSE 0 END,UpdatedAtUtc
FROM ReportingSmsMessages
WHERE MessageId=@MessageId AND UpdatedAtUtc>=@OccurredAtUtc AND UserId IS NOT NULL
  AND TenantId=@TenantId AND Provider=@Provider AND Direction=@Direction AND QueueStatus=@QueueStatus AND Status=@Status
ON CONFLICT (TenantId,UserId,ReportDate) DO UPDATE SET
    Username=EXCLUDED.Username,TotalMessages=UserSmsOverview.TotalMessages+1,
    Scheduled=UserSmsOverview.Scheduled+EXCLUDED.Scheduled,Queued=UserSmsOverview.Queued+EXCLUDED.Queued,
    Sent=UserSmsOverview.Sent+EXCLUDED.Sent,Delivered=UserSmsOverview.Delivered+EXCLUDED.Delivered,
    Failed=UserSmsOverview.Failed+EXCLUDED.Failed,Received=UserSmsOverview.Received+EXCLUDED.Received,
    Outbound=UserSmsOverview.Outbound+EXCLUDED.Outbound,Inbound=UserSmsOverview.Inbound+EXCLUDED.Inbound,
    Pending=UserSmsOverview.Pending+EXCLUDED.Pending,UpdatedAtUtc=GREATEST(UserSmsOverview.UpdatedAtUtc,EXCLUDED.UpdatedAtUtc);
