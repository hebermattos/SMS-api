using Sms.Application.Alerts;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Tests;

public sealed class AlertServiceTests
{
    [Fact]
    public async Task CreateRule_NormalizesAndPersistsTenantRule()
    {
        var repository = new FakeAlertRepository();
        var tenantId = Guid.NewGuid();
        var service = new AlertService(repository);

        var id = await service.CreateRuleAsync(tenantId,
            new(" Failure spike ", " Twilio ", SmsStatus.Failed, 10, 15, AlertRepeatMode.Once, 99, true));

        Assert.Equal(id, repository.Rule!.Id);
        Assert.Equal(tenantId, repository.Rule.TenantId);
        Assert.Equal("Failure spike", repository.Rule.Name);
        Assert.Equal("Twilio", repository.Rule.Provider);
        Assert.Null(repository.Rule.RepeatIntervalMinutes);
    }

    [Theory]
    [InlineData("", 10, 15, AlertRepeatMode.Once, null)]
    [InlineData("Rule", 0, 15, AlertRepeatMode.Once, null)]
    [InlineData("Rule", 10, 0, AlertRepeatMode.Once, null)]
    [InlineData("Rule", 10, 15, AlertRepeatMode.Repeating, null)]
    [InlineData("Rule", 10, 15, AlertRepeatMode.Repeating, 0)]
    public async Task CreateRule_RejectsInvalidConfiguration(
        string name, int threshold, int window, AlertRepeatMode mode, int? interval)
    {
        var service = new AlertService(new FakeAlertRepository());
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(Guid.NewGuid(),
            new(name, null, SmsStatus.Failed, threshold, window, mode, interval, true)));
    }

    [Fact]
    public async Task CreateRule_RejectsScheduledStatus()
    {
        var service = new AlertService(new FakeAlertRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRuleAsync(Guid.NewGuid(),
            new("Scheduled", null, SmsStatus.Scheduled, 1, 5, AlertRepeatMode.Once, null, true)));
    }

    [Fact]
    public async Task MissingTenantOwnedRecords_AreNotModified()
    {
        var service = new AlertService(new FakeAlertRepository());
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateRuleAsync(Guid.NewGuid(), Guid.NewGuid(),
            new("Rule", null, SmsStatus.Failed, 1, 5, AlertRepeatMode.Once, null, true)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteRuleAsync(Guid.NewGuid(), Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MarkReadAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    private sealed class FakeAlertRepository : IAlertRepository
    {
        public AlertRule? Rule { get; private set; }
        public Task<IReadOnlyList<AlertRule>> ListRulesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlertRule>>([]);
        public Task CreateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) { Rule = rule; return Task.CompletedTask; }
        public Task<bool> UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DeleteRuleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<AlertNotification>> ListAlertsAsync(Guid tenantId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlertNotification>>([]);
        public Task<bool> MarkReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task MarkAllReadAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EvaluateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
