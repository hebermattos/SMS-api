using System.Text.Json;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Providers;

public sealed class TwilioSettingsPolicy : IProviderSettingsPolicy
{
    public ProviderDefinition Definition => new("Twilio", "Account SID", "Auth Token", []);
    public string? MergeAndValidate(string? existing, IReadOnlyDictionary<string, string?> changes)
    {
        if (changes.Count != 0) throw new ArgumentException("Configuração não suportada para este provedor.");
        return existing;
    }
    public (IReadOnlyDictionary<string, string?>, IReadOnlyList<string>) Describe(string? settings) => (new Dictionary<string, string?>(), []);
}

public sealed class BandwidthSettingsPolicy : IProviderSettingsPolicy
{
    public ProviderDefinition Definition => new("Bandwidth", "OAuth Client ID", "OAuth Client Secret",
    [new("accountId", "Messaging Account ID", false, true), new("applicationId", "Application ID", false, true),
     new("webhookPassword", "Senha dos webhooks", true, true)]);

    public string MergeAndValidate(string? existing, IReadOnlyDictionary<string, string?> changes)
    {
        var values = Read(existing);
        foreach (var change in changes)
        {
            var field = Definition.Fields.FirstOrDefault(x => x.Key == change.Key)
                ?? throw new ArgumentException("Configuração não suportada para este provedor.");
            if (field.Secret && string.IsNullOrWhiteSpace(change.Value)) continue;
            values[field.Key] = field.Secret ? change.Value : change.Value?.Trim();
        }
        foreach (var field in Definition.Fields)
        {
            values.TryGetValue(field.Key, out var value);
            if (string.IsNullOrWhiteSpace(value) || value.Length > (field.Secret ? 512 : 200))
                throw new ArgumentException($"Preencha o campo {field.Label} com um valor válido.");
        }
        return JsonSerializer.Serialize(values);
    }

    public (IReadOnlyDictionary<string, string?>, IReadOnlyList<string>) Describe(string? settings)
    {
        var values = Read(settings);
        return (Definition.Fields.Where(x => !x.Secret).ToDictionary(x => x.Key, x => values.GetValueOrDefault(x.Key)),
            Definition.Fields.Where(x => x.Secret && !string.IsNullOrWhiteSpace(values.GetValueOrDefault(x.Key))).Select(x => x.Key).ToArray());
    }

    private static Dictionary<string, string?> Read(string? settings)
    {
        if (string.IsNullOrWhiteSpace(settings)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            return new(JsonSerializer.Deserialize<Dictionary<string, string?>>(settings) ?? new(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { throw new ArgumentException("As configurações existentes do provedor são inválidas."); }
    }
}
