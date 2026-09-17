using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Application.Providers;

namespace Sms.Infrastructure.Providers;

public sealed class TwilioSmsProvider(
    HttpClient httpClient,
    ITenantContext tenantContext,
    ITenantSmsProviderRepository configurations) : ISmsProvider
{
    public string Name => "Twilio";

    public async Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default)
    {
        var config = await configurations.GetAsync(tenantContext.TenantId, Name, cancellationToken)
            ?? throw new InvalidOperationException("Twilio is not configured for this tenant.");

        var configuredSender = config.FromNumber?.Trim();
        if (string.IsNullOrWhiteSpace(configuredSender))
            throw new InvalidOperationException("A Twilio From number is required.");
        if (!string.IsNullOrWhiteSpace(from) && !string.Equals(from.Trim(), configuredSender, StringComparison.Ordinal))
            throw new InvalidOperationException("The requested From number is not configured for this tenant.");
        var sender = configuredSender;

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"2010-04-01/Accounts/{Uri.EscapeDataString(config.AccountId)}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{config.AccountId}:{config.ApiSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = sender,
            ["To"] = to,
            ["Body"] = body
        });

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Twilio send failed with HTTP {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var sid = root.TryGetProperty("sid", out var sidElement) ? sidElement.GetString() : null;
        var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(sid))
            throw new InvalidOperationException("Twilio response did not contain a message SID.");

        return new ProviderSendResult(sid, status ?? "queued");
    }
}
