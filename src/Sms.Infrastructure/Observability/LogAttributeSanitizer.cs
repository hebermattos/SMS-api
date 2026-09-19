using System.Text.Json;

namespace Sms.Infrastructure.Observability;

internal static class LogAttributeSanitizer
{
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.Ordinal)
    {
        "TenantId",
        "MessageId",
        "Provider",
        "ClientId",
        "RequestMethod",
        "RequestPath",
        "StatusCode",
        "ElapsedMilliseconds",
        "Action",
        "Actor",
        "Outcome",
        "TargetTenantId",
        "TargetClientId",
        "TargetAdministratorId",
        "http.request.method",
        "http.response.status_code",
        "http.route",
        "server.address",
        "server.port",
        "network.protocol.version",
        "error.type"
    };

    internal static string? Serialize(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null || attributes.Count == 0) return null;

        var safe = attributes
            .Where(attribute => AllowedKeys.Contains(attribute.Key) && attribute.Value is not null)
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);

        return safe.Count == 0 ? null : JsonSerializer.Serialize(safe);
    }
}
