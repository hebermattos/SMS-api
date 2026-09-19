using Sms.Domain.Messages;

namespace Sms.Application.Alerts;

public enum AlertRepeatMode { Once = 1, Repeating = 2 }

public sealed record AlertRule(
    Guid Id, Guid TenantId, string Name, string? Provider, SmsStatus Status,
    int Threshold, int WindowMinutes, AlertRepeatMode RepeatMode,
    int? RepeatIntervalMinutes, bool IsActive, bool IsTriggered,
    DateTimeOffset? LastTriggeredAt, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record AlertNotification(
    Guid Id, Guid RuleId, string RuleName, string? Provider, SmsStatus Status,
    int MatchCount, int WindowMinutes, DateTimeOffset CreatedAt, bool IsRead,
    DateTimeOffset? ReadAt);

public sealed record SaveAlertRule(
    string Name, string? Provider, SmsStatus Status, int Threshold,
    int WindowMinutes, AlertRepeatMode RepeatMode, int? RepeatIntervalMinutes,
    bool IsActive);

public interface IAlertRepository
{
    Task<IReadOnlyList<AlertRule>> ListRulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task CreateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task<bool> UpdateRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task<bool> DeleteRuleAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertNotification>> ListAlertsAsync(Guid tenantId, bool unreadOnly, int skip, int take, CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EvaluateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
