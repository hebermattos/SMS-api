namespace Sms.Application.Messages;

public interface IMessageAssistant
{
    Task<MessageAssistantResult> ImproveAsync(string message, CancellationToken cancellationToken = default);
    Task<MessageAssistantResult> ValidateAsync(string message, CancellationToken cancellationToken = default);
}

public sealed record MessageAssistantResult(string Message, IReadOnlyList<string> Issues, bool IsValid);
