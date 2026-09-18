using Sms.Application.Messages;

namespace Sms.Infrastructure.Providers;

/// <summary>In-memory provider for local and automated flow tests. It never calls an external service.</summary>
public sealed class MockSmsProvider : ISmsProvider
{
    public string Name => "Mock";

    public Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var statuses = new[] { "queued", "sent", "delivered", "failed" };
        var status = statuses[Random.Shared.Next(statuses.Length)];
        return Task.FromResult(new ProviderSendResult($"mock-{Guid.NewGuid():N}", status));
    }
}
