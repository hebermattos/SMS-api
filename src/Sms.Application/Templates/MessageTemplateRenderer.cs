using System.Text.RegularExpressions;

namespace Sms.Application.Templates;

public static partial class MessageTemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*\}\}")]
    private static partial Regex VariableRegex();

    public static IReadOnlyList<string> Variables(string body) =>
        VariableRegex().Matches(body).Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static string Render(string body, IReadOnlyDictionary<string, string> values)
    {
        var lookup = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
        var missing = Variables(body).Where(name => !lookup.ContainsKey(name)).ToArray();
        if (missing.Length > 0)
            throw new ArgumentException($"Missing template variables: {string.Join(", ", missing)}.");

        return VariableRegex().Replace(body, match => lookup[match.Groups[1].Value]);
    }
}
