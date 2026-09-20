using Sms.Domain.Messages;

namespace Sms.Application.Alerts;

public sealed class AlertRuleFactory(TimeProvider clock)
{
    public AlertRule Create(Guid tenantId, SaveAlertRule request) =>
        Build(Guid.NewGuid(), tenantId, request);

    public AlertRule Update(Guid id, Guid tenantId, SaveAlertRule request) =>
        Build(id, tenantId, request);

    private AlertRule Build(Guid id, Guid tenantId, SaveAlertRule request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            throw new ArgumentException("Rule name is required and must not exceed 120 characters.");
        if (!Enum.IsDefined(request.Status))
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
        if (provider?.Length > 50)
            throw new ArgumentException("Provider must not exceed 50 characters.");

        return new AlertRule(
            id,
            tenantId,
            name,
            provider,
            request.Status,
            request.Threshold,
            request.WindowMinutes,
            request.RepeatMode,
            request.RepeatMode == AlertRepeatMode.Repeating ? request.RepeatIntervalMinutes : null,
            request.IsActive,
            false,
            null,
            clock.GetUtcNow(),
            null);
    }
}
