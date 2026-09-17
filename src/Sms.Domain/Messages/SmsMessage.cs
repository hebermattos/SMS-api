namespace Sms.Domain.Messages;

public enum SmsDirection { Outbound = 1, Inbound = 2 }
public enum SmsStatus { Queued = 1, Sent = 2, Delivered = 3, Failed = 4, Received = 5 }

public sealed class SmsMessage
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public SmsDirection Direction { get; init; }
    public SmsStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
