UPDATE dbo.TenantSmsOverview WITH (UPDLOCK, SERIALIZABLE)
SET Outbound = Outbound + @OutboundDelta,
    Inbound = Inbound + @InboundDelta,
    Delivered = Delivered + @DeliveredDelta,
    Failed = Failed + @FailedDelta,
    Pending = Pending + @PendingDelta,
    UpdatedAtUtc = @OccurredAtUtc
WHERE TenantId = @TenantId;

IF @@ROWCOUNT = 0
BEGIN
    INSERT dbo.TenantSmsOverview
        (TenantId, Outbound, Inbound, Delivered, Failed, Pending, UpdatedAtUtc)
    VALUES
        (@TenantId, @OutboundDelta, @InboundDelta, @DeliveredDelta, @FailedDelta, @PendingDelta, @OccurredAtUtc);
END;
