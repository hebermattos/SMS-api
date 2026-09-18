using Sms.Infrastructure.Persistence.Migrations;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer");
var migrationsDirectory = "database/migrations";
string? seedPath = null;

for (var index = 0; index < args.Length; index++)
{
    if (index + 1 >= args.Length) return Usage($"Missing value for '{args[index]}'.");
    var value = args[++index];
    switch (args[index - 1])
    {
        case "--connection": connectionString = value; break;
        case "--migrations": migrationsDirectory = value; break;
        case "--seed": seedPath = value; break;
        default: return Usage($"Unknown option '{args[index - 1]}'.");
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
    return Usage("ConnectionStrings__SqlServer or --connection is required.");

var migrator = new DatabaseMigrator(connectionString);
await migrator.MigrateAsync(migrationsDirectory);
if (seedPath is not null) await migrator.ApplySeedAsync(seedPath);
return 0;

static int Usage(string error)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine("Usage: Sms.Migrate [--connection <connection-string>] [--migrations <directory>] [--seed <sql-file>]");
    return 1;
}
