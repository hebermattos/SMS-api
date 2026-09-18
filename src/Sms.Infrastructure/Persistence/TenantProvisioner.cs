using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Auth;
using Sms.Application.Tenants;
using Sms.Application.Administration;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantProvisioner(SqlConnectionFactory connectionFactory) : ITenantProvisioner
{
    public async Task CreateAsync(Guid tenantId, string name, CreateApiClient client, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateSqlConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.Tenants (Id, Name, IsActive, CreatedAt) VALUES (@Id, @Name, 1, @Now);",
                new { Id = tenantId, Name = name, Now = now }, transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO dbo.ApiClients
                    (Id, TenantId, ClientId, SecretHash, SecretSalt, SecretIterations, IsActive, CreatedAt)
                VALUES
                    (@Id, @TenantId, @ClientId, @SecretHash, @SecretSalt, @SecretIterations, 1, @Now);
                """,
                new
                {
                    Id = Guid.NewGuid(), client.TenantId, client.ClientId, client.SecretHash,
                    client.SecretSalt, client.SecretIterations, Now = now
                }, transaction, cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new AdministrationConflictException();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
