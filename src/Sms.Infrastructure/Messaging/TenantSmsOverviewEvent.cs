namespace Sms.Infrastructure.Messaging;

public sealed record TenantSmsOverviewEvent(
    Guid EventId,
    Guid TenantId,
    long OutboundDelta,
    long InboundDelta,
    long DeliveredDelta,
    long FailedDelta,
    long PendingDelta,
    DateTimeOffset OccurredAtUtc);
