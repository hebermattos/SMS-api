SELECT TOP (100)
    EventId,
    TenantId,
    OutboundDelta,
    InboundDelta,
    DeliveredDelta,
    FailedDelta,
    PendingDelta,
    OccurredAtUtc
FROM dbo.TenantSmsOverviewOutbox WITH (READPAST, UPDLOCK, ROWLOCK)
WHERE PublishedAtUtc IS NULL
ORDER BY SequenceNumber;
