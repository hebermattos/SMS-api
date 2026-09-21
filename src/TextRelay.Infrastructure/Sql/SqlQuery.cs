using System.Collections.Concurrent;
using System.Reflection;

namespace Sms.Infrastructure.Sql;

public static class SqlQuery
{
    private const string ResourcePrefix = "TextRelay.Infrastructure.Sql.";
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string Load(string path) => Cache.GetOrAdd(path, static value =>
    {
        var resourceName = ResourcePrefix + value.Replace('/', '.');
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
