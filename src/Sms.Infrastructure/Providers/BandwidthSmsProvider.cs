using Sms.Application.Messages;

namespace Sms.Infrastructure.Providers;

public sealed class BandwidthSmsProvider : ISmsProvider
{
    public string Name => "Bandwidth";

    public Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Bandwidth credentials and transport are not configured yet.");
    }
}
