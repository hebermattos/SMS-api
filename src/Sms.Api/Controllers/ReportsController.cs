using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Common;
using Sms.Application.Reports;
using Sms.Domain.Messages;

namespace Sms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(ITenantContext tenantContext, ISmsReportRepository reports, ITenantTimeZoneProvider? timeZones = null) : ControllerBase
{
    [HttpGet("sms")]
    public async Task<SmsReportSummary> Sms(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] SmsStatus? status,
        [FromQuery] SmsDirection? direction,
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        var range = TenantDateRange.ToUtc(timeZones is null ? TimeZoneInfo.Utc : await timeZones.GetAsync(tenantContext.TenantId, cancellationToken), from, to);
        return await reports.GetTenantSummaryAsync(tenantContext.TenantId,
            new(range.From, range.To, status, direction, NormalizeProvider(provider)), cancellationToken);
    }

    [HttpGet("sms/users")]
    public Task<IReadOnlyList<UserSmsReportSummary>> SmsByUser(CancellationToken cancellationToken) =>
        reports.GetUserSummaryAsync(tenantContext.TenantId, cancellationToken);

    private static string? NormalizeProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();
}
