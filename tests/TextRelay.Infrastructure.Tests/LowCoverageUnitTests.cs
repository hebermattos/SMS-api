using Sms.Application.Reports;
using Sms.Application.Templates;

namespace Sms.Infrastructure.Tests;

public sealed class LowCoverageUnitTests
{
    [Fact]
    public void TemplateVariablesAreDistinctAndCaseInsensitive()
    {
        var variables = MessageTemplateRenderer.Variables("Hi {{ recipientName }}, {{Code}} {{code}}.");
        Assert.Equal(2, variables.Count);
        Assert.Contains("recipientName", variables);
        Assert.Contains("Code", variables);
    }

    [Fact]
    public void TemplateRenderCombinesCustomAndSystemValuesCaseInsensitively()
    {
        var result = MessageTemplateRenderer.Render(
            "{{recipientName}}: {{code}}",
            new Dictionary<string, string> { ["CODE"] = "123" },
            new Dictionary<string, string> { ["RecipientName"] = "Ana" });

        Assert.Equal("Ana: 123", result);
    }

    [Fact]
    public void TemplateRenderReportsAllMissingVariables()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            MessageTemplateRenderer.Render("{{first}} {{second}}", new Dictionary<string, string>()));

        Assert.Contains("first", error.Message);
        Assert.Contains("second", error.Message);
    }

    [Fact]
    public void SystemVariablesContainSupportedRecipientAndTenantValues()
    {
        Assert.Contains("recipientName", MessageTemplateRenderer.SystemVariables);
        Assert.Contains("recipientPhone", MessageTemplateRenderer.SystemVariables);
        Assert.Contains("tenantName", MessageTemplateRenderer.SystemVariables);
    }

    [Fact]
    public void ReportRecordsExposeEveryCounter()
    {
        var provider = new SmsReportProviderSummary("Twilio", 10, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        Assert.Equal(("Twilio", 10L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L),
            (provider.Provider, provider.TotalMessages, provider.Scheduled, provider.Queued, provider.Sent,
             provider.Delivered, provider.Failed, provider.Received, provider.Outbound, provider.Inbound, provider.Pending));

        var summary = new SmsReportSummary(10, 1, 2, 3, 4, 5, 6, 7, 8, 9, [provider]);
        Assert.Equal((10L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L),
            (summary.TotalMessages, summary.Scheduled, summary.Queued, summary.Sent, summary.Delivered,
             summary.Failed, summary.Received, summary.Outbound, summary.Inbound, summary.Pending));

        var tenant = new PlatformSmsReportTenantSummary(Guid.NewGuid(), "Tenant", 10, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        Assert.Equal((10L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L),
            (tenant.TotalMessages, tenant.Scheduled, tenant.Queued, tenant.Sent, tenant.Delivered,
             tenant.Failed, tenant.Received, tenant.Outbound, tenant.Inbound, tenant.Pending));

        var platform = new PlatformSmsReportSummary(10, 1, 2, 3, 4, 5, 6, 7, 8, 9, [tenant]);
        Assert.Equal((10L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L),
            (platform.TotalMessages, platform.Scheduled, platform.Queued, platform.Sent, platform.Delivered,
             platform.Failed, platform.Received, platform.Outbound, platform.Inbound, platform.Pending));

        var user = new UserSmsReportSummary(Guid.NewGuid(), "user", DateOnly.FromDateTime(DateTime.UtcNow),
            10, 1, 2, 3, 4, 5, 6, 7, 8, 9, DateTimeOffset.UtcNow);
        Assert.Equal((10L, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L),
            (user.TotalMessages, user.Scheduled, user.Queued, user.Sent, user.Delivered,
             user.Failed, user.Received, user.Outbound, user.Inbound, user.Pending));
    }
}
