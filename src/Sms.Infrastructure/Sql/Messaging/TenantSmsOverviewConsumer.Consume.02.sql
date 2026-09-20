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


INSERT INTO UserSmsOverview
    (TenantId, UserId, ReportDate, TotalMessages, Delivered, Failed, Pending, UpdatedAtUtc)
SELECT
    @TenantId,
    @UserId,
    CAST(@OccurredAtUtc AT TIME ZONE 'UTC' AS date),
    CASE WHEN @OutboundDelta > 0 THEN @OutboundDelta ELSE 0 END,
    @DeliveredDelta,
    @FailedDelta,
    @PendingDelta,
    @OccurredAtUtc
WHERE @UserId IS NOT NULL
ON CONFLICT (TenantId, UserId, ReportDate)
DO UPDATE SET
    TotalMessages=UserSmsOverview.TotalMessages + EXCLUDED.TotalMessages,
    Delivered=UserSmsOverview.Delivered + EXCLUDED.Delivered,
    Failed=UserSmsOverview.Failed + EXCLUDED.Failed,
    Pending=UserSmsOverview.Pending + EXCLUDED.Pending,
    UpdatedAtUtc=GREATEST(UserSmsOverview.UpdatedAtUtc, EXCLUDED.UpdatedAtUtc);
