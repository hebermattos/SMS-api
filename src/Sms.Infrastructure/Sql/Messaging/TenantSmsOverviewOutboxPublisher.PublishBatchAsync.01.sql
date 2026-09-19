SELECT
    EventId,
    TenantId,
    OutboundDelta,
    InboundDelta,
    DeliveredDelta,
    FailedDelta,
    PendingDelta,
    OccurredAtUtc
FROM TenantSmsOverviewOutbox
WHERE PublishedAtUtc IS NULL
ORDER BY SequenceNumber
LIMIT 100;
