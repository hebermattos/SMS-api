namespace Sms.Infrastructure.Persistence.Migrations;

public sealed record MigrationFile(string Name, string Path, string Content, string Sha256);
