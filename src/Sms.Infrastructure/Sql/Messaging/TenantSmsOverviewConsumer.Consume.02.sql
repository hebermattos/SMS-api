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

-- Persist the latest message state first. Daily aggregates are rebuilt from that state below,
-- so older events cannot overwrite a newer status.
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

DELETE FROM TenantSmsDailyOverview
WHERE TenantId=@TenantId AND ReportDate=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date);
DELETE FROM ProviderSmsDailyOverview
WHERE TenantId=@TenantId AND ReportDate=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date);
DELETE FROM UserSmsOverview
WHERE TenantId=@TenantId AND ReportDate=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date);

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
WHERE TenantId=@TenantId AND CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date)=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date)
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
WHERE TenantId=@TenantId AND CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date)=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date)
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
WHERE TenantId=@TenantId AND UserId IS NOT NULL
  AND CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date)=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date)
ON CONFLICT (TenantId,UserId,ReportDate) DO UPDATE SET
    Username=EXCLUDED.Username,TotalMessages=UserSmsOverview.TotalMessages+1,
    Scheduled=UserSmsOverview.Scheduled+EXCLUDED.Scheduled,Queued=UserSmsOverview.Queued+EXCLUDED.Queued,
    Sent=UserSmsOverview.Sent+EXCLUDED.Sent,Delivered=UserSmsOverview.Delivered+EXCLUDED.Delivered,
    Failed=UserSmsOverview.Failed+EXCLUDED.Failed,Received=UserSmsOverview.Received+EXCLUDED.Received,
    Outbound=UserSmsOverview.Outbound+EXCLUDED.Outbound,Inbound=UserSmsOverview.Inbound+EXCLUDED.Inbound,
    Pending=UserSmsOverview.Pending+EXCLUDED.Pending,UpdatedAtUtc=GREATEST(UserSmsOverview.UpdatedAtUtc,EXCLUDED.UpdatedAtUtc);
