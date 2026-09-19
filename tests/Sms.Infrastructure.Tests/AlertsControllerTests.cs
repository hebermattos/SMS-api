using Microsoft.AspNetCore.Mvc;
using Sms.Api.Controllers;
using Sms.Application.Alerts;
using Sms.Application.Common;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Tests;

public sealed class AlertsControllerTests
{
    [Fact]
    public async Task EndpointsUseOnlyAuthenticatedTenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new Repository();
        var controller = new AlertsController(new TenantContext(tenantId), new AlertService(repository));
        var request = new SaveAlertRule("Failures", "Twilio", SmsStatus.Failed, 2, 15, AlertRepeatMode.Once, null, true);

        var created = Assert.IsType<CreatedResult>(await controller.CreateRule(request, default));
        Assert.StartsWith("/api/v1/alerts/rules/", created.Location);
        Assert.Equal(tenantId, repository.TenantId);
        var id = repository.Rule!.Id;

        Assert.Single(await controller.Rules(default));
        await controller.UpdateRule(id, request with { Threshold = 3 }, default);
        Assert.Equal(3, repository.Rule.Threshold);
        Assert.Empty(await controller.List(cancellationToken: default));
        await controller.MarkRead(Guid.NewGuid(), default);
        await controller.MarkAllRead(default);
        await controller.DeleteRule(id, default);
        Assert.Empty(await controller.Rules(default));
    }

    private sealed record TenantContext(Guid TenantId) : ITenantContext;

    private sealed class Repository : IAlertRepository
    {
        public Guid TenantId { get; private set; }
        public AlertRule? Rule { get; private set; }
        public Task<IReadOnlyList<AlertRule>> ListRulesAsync(Guid tenantId, CancellationToken cancellationToken = default) { TenantId = tenantId; return Task.FromResult<IReadOnlyList<AlertRule>>(Rule is null ? [] : [Rule]); }
        public Task CreateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) { TenantId = rule.TenantId; Rule = rule; return Task.CompletedTask; }
        public Task<bool> UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) { TenantId = rule.TenantId; Rule = rule; return Task.FromResult(true); }
        public Task<bool> DeleteRuleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) { TenantId = tenantId; Rule = null; return Task.FromResult(true); }
        public Task<IReadOnlyList<AlertNotification>> ListAlertsAsync(Guid tenantId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken = default) { TenantId = tenantId; return Task.FromResult<IReadOnlyList<AlertNotification>>([]); }
        public Task<bool> MarkReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) { TenantId = tenantId; return Task.FromResult(true); }
        public Task MarkAllReadAsync(Guid tenantId, CancellationToken cancellationToken = default) { TenantId = tenantId; return Task.CompletedTask; }
        public Task EvaluateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
