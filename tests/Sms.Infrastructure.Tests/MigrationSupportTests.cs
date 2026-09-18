using Sms.Infrastructure.Persistence.Migrations;

namespace Sms.Infrastructure.Tests;

public sealed class MigrationSupportTests
{
    [Fact]
    public void SqlBatchSplitter_SplitsStandaloneGoAndIgnoresEmptyBatches()
    {
        var batches = SqlBatchSplitter.Split("SELECT 'GO inside text';\nGO\n\n go \nSELECT 2;");

        Assert.Equal(2, batches.Count);
        Assert.Contains("GO inside text", batches[0]);
        Assert.Equal("SELECT 2;", batches[1]);
    }

    [Fact]
    public void MigrationCatalog_LoadsSqlFilesInOrdinalOrderWithHashes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sms-migrations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "002_second.sql"), "SELECT 2;");
            File.WriteAllText(Path.Combine(directory, "001_first.sql"), "SELECT 1;");

            var migrations = MigrationCatalog.Load(directory);

            Assert.Equal(["001_first.sql", "002_second.sql"], migrations.Select(x => x.Name));
            Assert.All(migrations, x => Assert.Equal(64, x.Sha256.Length));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MigrationCatalog_RejectsMissingDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(() => MigrationCatalog.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}
