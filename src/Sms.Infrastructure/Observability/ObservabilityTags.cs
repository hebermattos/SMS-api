using System.Text.Json;

namespace Sms.Infrastructure.Observability;

internal static class ObservabilityTags
{
    private static readonly HashSet<string> Allowed =
    [
        "http.request.method",
        "http.response.status_code",
        "http.route",
        "server.address",
        "server.port",
        "network.protocol.version",
        "error.type"
    ];

    internal static string? Serialize(OpenTelemetry.ReadOnlyTagCollection tags)
    {
        var values = new Dictionary<string, object?>();
        foreach (var tag in tags) values[tag.Key] = tag.Value;
        return Serialize(values);
    }

    internal static string? Serialize(IEnumerable<KeyValuePair<string, object?>> tags)
    {
        var safe = tags
            .Where(tag => Allowed.Contains(tag.Key) && tag.Value is not null)
            .ToDictionary(tag => tag.Key, tag => tag.Value);
        return safe.Count == 0 ? null : JsonSerializer.Serialize(safe);
    }
}
