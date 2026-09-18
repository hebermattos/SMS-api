using System.Security.Cryptography;
using System.Text;

namespace Sms.Infrastructure.Persistence.Migrations;

public static class MigrationCatalog
{
    public static IReadOnlyList<MigrationFile> Load(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A migrations directory is required.", nameof(directory));
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"Migrations directory '{directory}' was not found.");

        return Directory.GetFiles(directory, "*.sql", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path =>
            {
                var content = File.ReadAllText(path);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
                return new MigrationFile(Path.GetFileName(path), path, content, hash);
            })
            .ToArray();
    }
}
