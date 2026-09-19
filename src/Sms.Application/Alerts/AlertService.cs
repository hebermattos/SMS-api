using Sms.Domain.Messages;

namespace Sms.Application.Alerts;

public sealed class AlertService(IAlertRepository repository, TimeProvider clock)
{
    public Task<IReadOnlyList<AlertRule>> ListRulesAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        repository.ListRulesAsync(tenantId, cancellationToken);

    public async Task<Guid> CreateRuleAsync(Guid tenantId, SaveAlertRule request, CancellationToken cancellationToken = default)
    {
        var rule = CreateValidatedRule(Guid.NewGuid(), tenantId, request, clock.GetUtcNow());
        await repository.CreateRuleAsync(rule, cancellationToken);
        return rule.Id;
    }

    public async Task UpdateRuleAsync(Guid tenantId, Guid id, SaveAlertRule request, CancellationToken cancellationToken = default)
    {
        var rule = CreateValidatedRule(id, tenantId, request, clock.GetUtcNow());
        if (!await repository.UpdateRuleAsync(rule, cancellationToken))
            throw new KeyNotFoundException("Alert rule was not found.");
    }

    public async Task DeleteRuleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteRuleAsync(tenantId, id, cancellationToken))
            throw new KeyNotFoundException("Alert rule was not found.");
    }

    public Task<IReadOnlyList<AlertNotification>> ListAlertsAsync(
        Guid tenantId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken = default)
    {
        if (skip < 0 || take is < 1 or > 200) throw new ArgumentException("Invalid pagination.");
        return repository.ListAlertsAsync(tenantId, unreadOnly, skip, take, cancellationToken);
    }

    public async Task MarkReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        if (!await repository.MarkReadAsync(tenantId, id, cancellationToken))
            throw new KeyNotFoundException("Alert was not found.");
    }

    public Task MarkAllReadAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        repository.MarkAllReadAsync(tenantId, cancellationToken);

    internal static AlertRule CreateValidatedRule(Guid id, Guid tenantId, SaveAlertRule request, DateTimeOffset now)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            throw new ArgumentException("Rule name is required and must not exceed 120 characters.");
        if (!Enum.IsDefined(request.Status) || request.Status == SmsStatus.Scheduled)
            throw new ArgumentException("A valid SMS status is required.");
        if (request.Threshold is < 1 or > 1_000_000)
            throw new ArgumentException("Threshold must be between 1 and 1,000,000.");
        if (request.WindowMinutes is < 1 or > 43_200)
            throw new ArgumentException("Time window must be between 1 minute and 30 days.");
        if (!Enum.IsDefined(request.RepeatMode))
            throw new ArgumentException("A valid repeat mode is required.");
        if (request.RepeatMode == AlertRepeatMode.Repeating
            && request.RepeatIntervalMinutes is not (>= 1 and <= 43_200))
            throw new ArgumentException("Repeat interval must be between 1 minute and 30 days.");
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider.Trim();
        if (provider?.Length > 50) throw new ArgumentException("Provider must not exceed 50 characters.");

        return new(id, tenantId, name, provider, request.Status, request.Threshold,
            request.WindowMinutes, request.RepeatMode,
            request.RepeatMode == AlertRepeatMode.Repeating ? request.RepeatIntervalMinutes : null,
            request.IsActive, false, null, now, null);
    }
}
