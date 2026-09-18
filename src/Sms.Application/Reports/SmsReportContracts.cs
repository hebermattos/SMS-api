using Sms.Domain.Messages;

namespace Sms.Application.Reports;

public sealed record SmsReportFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    SmsStatus? Status,
    SmsDirection? Direction,
    string? Provider);

public sealed record SmsReportSummary(
    long TotalMessages,
    long Queued,
    long Sent,
    long Delivered,
    long Failed,
    long Received,
    long Outbound,
    long Inbound,
    IReadOnlyList<SmsReportProviderSummary> ByProvider);

public sealed record SmsReportProviderSummary(
    string Provider,
    long TotalMessages,
    long Delivered,
    long Failed);

public sealed record PlatformSmsReportSummary(
    long TotalMessages,
    long Queued,
    long Sent,
    long Delivered,
    long Failed,
    long Received,
    IReadOnlyList<PlatformSmsReportTenantSummary> ByTenant);

public sealed record PlatformSmsReportTenantSummary(
    Guid TenantId,
    string TenantName,
    long TotalMessages,
    long Queued,
    long Sent,
    long Delivered,
    long Failed,
    long Received);
