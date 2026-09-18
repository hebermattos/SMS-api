using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Common;
using Sms.Application.Reports;
using Sms.Domain.Messages;

namespace Sms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(ITenantContext tenantContext, ISmsReportRepository reports) : ControllerBase
{
    [HttpGet("sms")]
    public Task<SmsReportSummary> Sms(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] SmsStatus? status,
        [FromQuery] SmsDirection? direction,
        [FromQuery] string? provider,
        CancellationToken cancellationToken) =>
        reports.GetTenantSummaryAsync(tenantContext.TenantId,
            new(from, to, status, direction, NormalizeProvider(provider)), cancellationToken);

    private static string? NormalizeProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();
}
