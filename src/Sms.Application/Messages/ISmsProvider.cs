namespace Sms.Application.Messages;

public sealed record SendSmsRequest(string To, string Body, string? From = null, string? Provider = null);
public sealed record SendSmsResult(Guid Id, string Provider, string? ProviderMessageId, string Status);

public interface ISmsProvider
{
    string Name { get; }
    Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default);
}

public sealed record ProviderSendResult(string ProviderMessageId, string Status);
