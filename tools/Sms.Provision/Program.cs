using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Auth;
using Microsoft.Extensions.Configuration;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Sql;

if (args is ["--admin"])
{
    var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var username = configuration["Admin:Username"] ?? throw new InvalidOperationException("Admin:Username is required.");
    var password = configuration["Admin:Password"] ?? throw new InvalidOperationException("Admin:Password is required.");
    var email = configuration["Admin:Email"] ?? throw new InvalidOperationException("Admin:Email is required.");
    var administrators = new AdministratorRepository(new SqlConnectionFactory(configuration));
    if (await administrators.GetByUsernameAsync(username.Trim()) is not null)
    {
        Console.Error.WriteLine("This administrator username already exists. No credentials were changed.");
        return 1;
    }
    var id = await new AdministratorAuthenticationService(administrators).CreateAsync(username, email, password);
    Console.WriteLine($"Administrator created: {id}");
    return 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/Sms.Provision -- <connection-string> <tenant-name> [client-id]");
    Console.Error.WriteLine("Administrator: dotnet run --project tools/Sms.Provision -- --admin (uses ConnectionStrings__SqlServer, Admin__Username and Admin__Password environment variables)");
    return 1;
}

var connectionString = args[0];
var tenantName = args[1].Trim();
var tenantId = Guid.NewGuid();
var clientId = args.Length > 2 ? args[2].Trim() : $"tenant_{tenantId:N}";
var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
var hashed = ClientSecretHasher.Hash(secret);

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteAsync(SqlQuery.Load("Provision/CreateTenant.sql"),
        new { Id = tenantId, Name = tenantName, Now = DateTimeOffset.UtcNow }, transaction);
    await connection.ExecuteAsync(SqlQuery.Load("Provision/CreateApiClient.sql"),
        new { Id = Guid.NewGuid(), TenantId = tenantId, ClientId = clientId, SecretHash = hashed.Hash, SecretSalt = hashed.Salt, SecretIterations = hashed.Iterations, Now = DateTimeOffset.UtcNow }, transaction);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

Console.WriteLine($"TenantId: {tenantId}");
Console.WriteLine($"ClientId: {clientId}");
Console.WriteLine($"ClientSecret: {secret}");
Console.WriteLine("Store ClientSecret now. It cannot be recovered.");
return 0;
