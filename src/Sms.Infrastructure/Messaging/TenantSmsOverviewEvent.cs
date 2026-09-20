namespace Sms.Infrastructure.Messaging;

public sealed record TenantSmsOverviewEvent(
    Guid EventId,
    Guid TenantId,
    Guid? UserId,
    long OutboundDelta,
    long InboundDelta,
    long DeliveredDelta,
    long FailedDelta,
    long PendingDelta,
    DateTimeOffset OccurredAtUtc);
