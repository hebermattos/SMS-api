using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Application.Providers;

namespace Sms.Infrastructure.Providers;

public sealed class BandwidthSmsProvider(
    HttpClient messagingClient,
    IHttpClientFactory httpClientFactory,
    ITenantContext tenantContext,
    ITenantSmsProviderRepository configurations) : ISmsProvider
{
    public string Name => "Bandwidth";

    public async Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default)
    {
        var config = await configurations.GetAsync(tenantContext.TenantId, Name, cancellationToken)
            ?? throw new InvalidOperationException("Bandwidth is not configured for this tenant.");
        var settings = ParseSettings(config.Settings);
        var sender = string.IsNullOrWhiteSpace(from) ? config.FromNumber : from;
        if (string.IsNullOrWhiteSpace(sender)) throw new InvalidOperationException("Bandwidth From number is not configured.");
        if (!string.IsNullOrWhiteSpace(config.FromNumber) && !string.Equals(sender, config.FromNumber, StringComparison.Ordinal))
            throw new InvalidOperationException("The requested From number is not configured for this tenant.");

        var token = await GetAccessTokenAsync(config.AccountId, config.ApiSecret, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v2/users/{Uri.EscapeDataString(settings.AccountId)}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { to = new[] { to }, from = sender, text = body, applicationId = settings.ApplicationId });
        using var response = await messagingClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Bandwidth returned HTTP {(int)response.StatusCode}.");

        var result = await response.Content.ReadFromJsonAsync<BandwidthMessageResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.Id)) throw new InvalidOperationException("Bandwidth response did not include a message id.");
        return new ProviderSendResult(result.Id, "queued");
    }

    private async Task<string> GetAccessTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("BandwidthOAuth");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"] = "client_credentials" });
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"Bandwidth OAuth returned HTTP {(int)response.StatusCode}.");
        var token = await response.Content.ReadFromJsonAsync<OAuthResponse>(cancellationToken: cancellationToken);
        return string.IsNullOrWhiteSpace(token?.AccessToken) ? throw new InvalidOperationException("Bandwidth OAuth response did not include an access token.") : token.AccessToken;
    }

    private static BandwidthSettings ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Bandwidth provider settings are required.");
        var settings = JsonSerializer.Deserialize<BandwidthSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (string.IsNullOrWhiteSpace(settings?.AccountId) || string.IsNullOrWhiteSpace(settings.ApplicationId))
            throw new InvalidOperationException("Bandwidth settings must contain accountId and applicationId.");
        return settings;
    }

    private sealed record BandwidthSettings(string AccountId, string ApplicationId);
    private sealed record OAuthResponse([property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);
    private sealed record BandwidthMessageResponse(string Id);
}
