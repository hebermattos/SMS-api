using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Auth;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/Sms.Provision -- <connection-string> <tenant-name> [client-id]");
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
    await connection.ExecuteAsync("INSERT INTO dbo.Tenants (Id, Name, IsActive, CreatedAt) VALUES (@Id,@Name,1,@Now);",
        new { Id = tenantId, Name = tenantName, Now = DateTimeOffset.UtcNow }, transaction);
    await connection.ExecuteAsync("""
        INSERT INTO dbo.ApiClients (Id,TenantId,ClientId,SecretHash,SecretSalt,SecretIterations,IsActive,CreatedAt)
        VALUES (@Id,@TenantId,@ClientId,@SecretHash,@SecretSalt,@SecretIterations,1,@Now);
        """, new { Id = Guid.NewGuid(), TenantId = tenantId, ClientId = clientId, SecretHash = hashed.Hash, SecretSalt = hashed.Salt, SecretIterations = hashed.Iterations, Now = DateTimeOffset.UtcNow }, transaction);
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
