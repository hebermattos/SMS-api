using Sms.Application.Messages;

namespace Sms.Infrastructure.Providers;

public sealed class TwilioSmsProvider : ISmsProvider
{
    public string Name => "Twilio";

    public Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Twilio credentials and transport are not configured yet.");
    }
}
