using Dapper;
using Microsoft.Data.SqlClient;

namespace Sms.Infrastructure.Persistence.Migrations;

public sealed class DatabaseMigrator(string connectionString)
{
    private static readonly IReadOnlyDictionary<string, string> LegacyMigrationChecks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["001_initial.sql"] = "SELECT IIF(OBJECT_ID(N'dbo.Tenants', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SmsMessages', N'U') IS NOT NULL, 1, 0);",
        ["002_tenant_providers.sql"] = "SELECT IIF(OBJECT_ID(N'dbo.TenantSmsProviders', N'U') IS NOT NULL, 1, 0);",
        ["003_api_clients.sql"] = "SELECT IIF(OBJECT_ID(N'dbo.ApiClients', N'U') IS NOT NULL, 1, 0);",
        ["004_provider_settings.sql"] = "SELECT IIF(COL_LENGTH(N'dbo.TenantSmsProviders', N'Settings') IS NOT NULL, 1, 0);",
        ["005_message_status_history.sql"] = "SELECT IIF(OBJECT_ID(N'dbo.SmsMessageStatusHistory', N'U') IS NOT NULL, 1, 0);",
        ["006_shared_provider_account_routing.sql"] = "SELECT IIF(INDEXPROPERTY(OBJECT_ID(N'dbo.TenantSmsProviders'), N'UX_TenantSmsProviders_CallbackRoute', N'IndexId') IS NOT NULL, 1, 0);"
    };

    public async Task MigrateAsync(string migrationsDirectory, CancellationToken cancellationToken = default)
    {
        var migrations = MigrationCatalog.Load(migrationsDirectory);
        if (migrations.Count == 0) throw new InvalidOperationException("No SQL migrations were found.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new InvalidOperationException("The SQL Server connection string must specify a database.");

        await EnsureDatabaseAsync(builder, cancellationToken);
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureHistoryTableAsync(connection, cancellationToken);
        await BaselineLegacySchemaAsync(connection, migrations, cancellationToken);

        var applied = (await connection.QueryAsync<AppliedMigration>(new CommandDefinition(
            "SELECT Name, ContentHash FROM dbo.SchemaMigrations;",
            cancellationToken: cancellationToken))).ToDictionary(x => x.Name, StringComparer.Ordinal);

        foreach (var migration in migrations)
        {
            if (applied.TryGetValue(migration.Name, out var existing))
            {
                if (!existing.ContentHash.Equals(migration.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Applied migration '{migration.Name}' has been modified.");
                continue;
            }

            await ApplyMigrationAsync(connection, migration, cancellationToken);
            Console.WriteLine($"Applied migration {migration.Name}");
        }
    }

    public async Task ApplySeedAsync(string seedPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(seedPath)) throw new FileNotFoundException("Seed SQL file was not found.", seedPath);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var batch in SqlBatchSplitter.Split(await File.ReadAllTextAsync(seedPath, cancellationToken)))
            await connection.ExecuteAsync(new CommandDefinition(batch, cancellationToken: cancellationToken));
    }

    private static async Task EnsureDatabaseAsync(SqlConnectionStringBuilder target, CancellationToken cancellationToken)
    {
        var databaseName = target.InitialCatalog;
        var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            IF DB_ID(@DatabaseName) IS NULL
            BEGIN
                DECLARE @statement NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName);
                EXEC sp_executesql @statement;
            END
            """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new { DatabaseName = databaseName }, cancellationToken: cancellationToken));
    }

    private static Task EnsureHistoryTableAsync(SqlConnection connection, CancellationToken cancellationToken) => connection.ExecuteAsync(new CommandDefinition("""
        IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SchemaMigrations
            (
                Name NVARCHAR(260) NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
                ContentHash CHAR(64) NOT NULL,
                AppliedAt DATETIMEOFFSET NOT NULL
            );
        END
        """, cancellationToken: cancellationToken));

    private static async Task BaselineLegacySchemaAsync(SqlConnection connection, IReadOnlyList<MigrationFile> migrations, CancellationToken cancellationToken)
    {
        var historyCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM dbo.SchemaMigrations;", cancellationToken: cancellationToken));
        if (historyCount > 0) return;

        foreach (var migration in migrations)
        {
            if (!LegacyMigrationChecks.TryGetValue(migration.Name, out var check)) continue;
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(check, cancellationToken: cancellationToken));
            if (exists != 1) continue;

            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT dbo.SchemaMigrations(Name, ContentHash, AppliedAt) VALUES(@Name, @ContentHash, @AppliedAt);",
                new { migration.Name, ContentHash = migration.Sha256, AppliedAt = DateTimeOffset.UtcNow },
                cancellationToken: cancellationToken));
            Console.WriteLine($"Baselined existing migration {migration.Name}");
        }
    }

    private static async Task ApplyMigrationAsync(SqlConnection connection, MigrationFile migration, CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in SqlBatchSplitter.Split(migration.Content))
                await connection.ExecuteAsync(new CommandDefinition(batch, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT dbo.SchemaMigrations(Name, ContentHash, AppliedAt) VALUES(@Name, @ContentHash, @AppliedAt);",
                new { migration.Name, ContentHash = migration.Sha256, AppliedAt = DateTimeOffset.UtcNow },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private sealed record AppliedMigration(string Name, string ContentHash);
}
