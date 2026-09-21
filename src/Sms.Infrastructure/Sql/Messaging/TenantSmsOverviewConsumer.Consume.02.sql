-- Serialize updates for the same message. The current reporting row is the source of truth.
SELECT pg_advisory_xact_lock(hashtextextended(@MessageId::text, 0));

WITH previous AS (
    SELECT Direction, QueueStatus, Status, UserId, Provider,
           CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date) AS ReportDate,
           UpdatedAtUtc
    FROM ReportingSmsMessages
    WHERE MessageId=@MessageId
),
accepted AS (
    INSERT INTO ReportingSmsMessages
        (MessageId,TenantId,TenantName,UserId,Username,Provider,Direction,QueueStatus,Status,CreatedAtUtc,UpdatedAtUtc)
    VALUES
        (@MessageId,@TenantId,@TenantName,@UserId,@Username,@Provider,@Direction,@QueueStatus,@Status,@CreatedAtUtc,@OccurredAtUtc)
    ON CONFLICT (MessageId) DO UPDATE SET
        TenantName=EXCLUDED.TenantName,UserId=EXCLUDED.UserId,Username=EXCLUDED.Username,
        Provider=EXCLUDED.Provider,Direction=EXCLUDED.Direction,QueueStatus=EXCLUDED.QueueStatus,
        Status=EXCLUDED.Status,UpdatedAtUtc=EXCLUDED.UpdatedAtUtc
    WHERE EXCLUDED.UpdatedAtUtc > ReportingSmsMessages.UpdatedAtUtc
    RETURNING 1
),
delta AS (
    SELECT
      CASE WHEN EXISTS(SELECT 1 FROM previous) THEN 0 ELSE 1 END AS TotalDelta,
      (CASE WHEN @QueueStatus=3 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN QueueStatus=3 THEN 1 ELSE 0 END FROM previous),0)) AS ScheduledDelta,
      (CASE WHEN @QueueStatus=2 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN QueueStatus=2 THEN 1 ELSE 0 END FROM previous),0)) AS QueuedDelta,
      (CASE WHEN @Status=2 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN Status=2 THEN 1 ELSE 0 END FROM previous),0)) AS SentDelta,
      (CASE WHEN @Status=3 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN Status=3 THEN 1 ELSE 0 END FROM previous),0)) AS DeliveredDelta,
      (CASE WHEN @Status=4 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN Status=4 THEN 1 ELSE 0 END FROM previous),0)) AS FailedDelta,
      (CASE WHEN @Status=5 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN Status=5 THEN 1 ELSE 0 END FROM previous),0)) AS ReceivedDelta,
      (CASE WHEN @Direction=1 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN Direction=1 THEN 1 ELSE 0 END FROM previous),0)) AS OutboundDelta,
      (CASE WHEN @Direction=2 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN Direction=2 THEN 1 ELSE 0 END FROM previous),0)) AS InboundDelta,
      (CASE WHEN @Status=1 THEN 1 ELSE 0 END - COALESCE((SELECT CASE WHEN Status=1 THEN 1 ELSE 0 END FROM previous),0)) AS PendingDelta
    WHERE EXISTS(SELECT 1 FROM accepted)
)
INSERT INTO TenantSmsOverview (TenantId,Outbound,Inbound,Delivered,Failed,Pending,UpdatedAtUtc)
SELECT @TenantId,OutboundDelta,InboundDelta,DeliveredDelta,FailedDelta,PendingDelta,@OccurredAtUtc FROM delta
ON CONFLICT (TenantId) DO UPDATE SET
 Outbound=TenantSmsOverview.Outbound+EXCLUDED.Outbound,Inbound=TenantSmsOverview.Inbound+EXCLUDED.Inbound,
 Delivered=TenantSmsOverview.Delivered+EXCLUDED.Delivered,Failed=TenantSmsOverview.Failed+EXCLUDED.Failed,
 Pending=TenantSmsOverview.Pending+EXCLUDED.Pending,UpdatedAtUtc=GREATEST(TenantSmsOverview.UpdatedAtUtc,EXCLUDED.UpdatedAtUtc);

-- Daily projections use the same accepted current-state transition. Rebuild is deliberately avoided.
WITH current AS (
 SELECT * FROM ReportingSmsMessages WHERE MessageId=@MessageId AND UpdatedAtUtc=@OccurredAtUtc
), old AS (
 SELECT 0 AS dummy
)
INSERT INTO TenantSmsDailyOverview
(ReportDate,TenantId,TenantName,TotalMessages,Scheduled,Queued,Sent,Delivered,Failed,Received,Outbound,Inbound,Pending,UpdatedAtUtc)
SELECT CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date),TenantId,TenantName,1,
 CASE WHEN QueueStatus=3 THEN 1 ELSE 0 END,CASE WHEN QueueStatus=2 THEN 1 ELSE 0 END,
 CASE WHEN Status=2 THEN 1 ELSE 0 END,CASE WHEN Status=3 THEN 1 ELSE 0 END,CASE WHEN Status=4 THEN 1 ELSE 0 END,
 CASE WHEN Status=5 THEN 1 ELSE 0 END,CASE WHEN Direction=1 THEN 1 ELSE 0 END,CASE WHEN Direction=2 THEN 1 ELSE 0 END,
 CASE WHEN Status=1 THEN 1 ELSE 0 END,UpdatedAtUtc FROM current
ON CONFLICT (ReportDate,TenantId) DO UPDATE SET UpdatedAtUtc=GREATEST(TenantSmsDailyOverview.UpdatedAtUtc,EXCLUDED.UpdatedAtUtc);

-- Keep provider/user aggregates convergent by rebuilding only the affected message dimensions from current state.
-- Their rows are recomputed with indexed scans; no full tenant/day scan is needed for the tenant overview.
DELETE FROM ProviderSmsDailyOverview WHERE TenantId=@TenantId AND ReportDate=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date);
INSERT INTO ProviderSmsDailyOverview
(ReportDate,TenantId,TenantName,Provider,TotalMessages,Scheduled,Queued,Sent,Delivered,Failed,Received,Outbound,Inbound,Pending,UpdatedAtUtc)
SELECT CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date),TenantId,MAX(TenantName),Provider,COUNT(*),
 SUM((QueueStatus=3)::int),SUM((QueueStatus=2)::int),SUM((Status=2)::int),SUM((Status=3)::int),SUM((Status=4)::int),
 SUM((Status=5)::int),SUM((Direction=1)::int),SUM((Direction=2)::int),SUM((Status=1)::int),MAX(UpdatedAtUtc)
FROM ReportingSmsMessages WHERE TenantId=@TenantId AND CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date)=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date)
GROUP BY TenantId,Provider,CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date);

DELETE FROM UserSmsOverview WHERE TenantId=@TenantId AND ReportDate=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date);
INSERT INTO UserSmsOverview
(TenantId,UserId,Username,ReportDate,TotalMessages,Scheduled,Queued,Sent,Delivered,Failed,Received,Outbound,Inbound,Pending,UpdatedAtUtc)
SELECT TenantId,UserId,MAX(COALESCE(Username,'')),CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date),COUNT(*),
 SUM((QueueStatus=3)::int),SUM((QueueStatus=2)::int),SUM((Status=2)::int),SUM((Status=3)::int),SUM((Status=4)::int),
 SUM((Status=5)::int),SUM((Direction=1)::int),SUM((Direction=2)::int),SUM((Status=1)::int),MAX(UpdatedAtUtc)
FROM ReportingSmsMessages WHERE TenantId=@TenantId AND UserId IS NOT NULL AND CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date)=CAST(@CreatedAtUtc AT TIME ZONE 'UTC' AS date)
GROUP BY TenantId,UserId,CAST(CreatedAtUtc AT TIME ZONE 'UTC' AS date);
