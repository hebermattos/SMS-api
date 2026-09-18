using System.Text.Json;
using Sms.Infrastructure.Observability;

namespace Sms.Infrastructure.Tests;

public sealed class ObservabilityTagsTests
{
    [Fact]
    public void Serialize_StoresOnlyApprovedAttributes()
    {
        var result = ObservabilityTags.Serialize(new Dictionary<string, object?>
        {
            ["http.request.method"] = "POST",
            ["http.response.status_code"] = 202,
            ["url.query"] = "token=secret",
            ["authorization"] = "Bearer secret",
            ["phone.number"] = "+15551234567"
        });

        using var json = JsonDocument.Parse(result!);
        Assert.Equal("POST", json.RootElement.GetProperty("http.request.method").GetString());
        Assert.Equal(202, json.RootElement.GetProperty("http.response.status_code").GetInt32());
        Assert.False(json.RootElement.TryGetProperty("url.query", out _));
        Assert.False(json.RootElement.TryGetProperty("authorization", out _));
        Assert.False(json.RootElement.TryGetProperty("phone.number", out _));
    }

    [Fact]
    public void Serialize_ReturnsNullWhenNoApprovedAttributesExist()
    {
        var result = ObservabilityTags.Serialize(new Dictionary<string, object?>
        {
            ["url.query"] = "client_secret=value"
        });

        Assert.Null(result);
    }
}
