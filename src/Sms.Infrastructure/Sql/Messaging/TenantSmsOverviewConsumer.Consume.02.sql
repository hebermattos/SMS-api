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
