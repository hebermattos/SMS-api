namespace Sms.Infrastructure.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerTestCollection
{
    public const string Name = "SqlServer";
}
