using System.Diagnostics;

namespace Sms.Infrastructure.Observability;

public static class TextRelayTelemetry
{
    public const string ActivitySourceName = "TextRelay";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
