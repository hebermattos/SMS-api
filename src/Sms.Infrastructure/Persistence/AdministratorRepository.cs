using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class AdministratorRepository(SqlConnectionFactory connections) : IAdministratorRepository
{
    public async Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AdministratorAccount>(new CommandDefinition(
            "SELECT Id,Username,PasswordHash,PasswordSalt,PasswordIterations,IsActive FROM dbo.PlatformAdministrators WHERE Username=@Username;",
            new { Username = username }, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.PlatformAdministrators WHERE Id=@Id AND IsActive=1) THEN 1 ELSE 0 END AS BIT);",
            new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return (await connection.QueryAsync<AdministratorSummary>(new CommandDefinition("""
            SELECT Id,Username,IsActive,CreatedAt FROM dbo.PlatformAdministrators ORDER BY Username;
            """, cancellationToken: cancellationToken))).AsList();
    }

    public async Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT dbo.PlatformAdministrators(Id,Username,PasswordHash,PasswordSalt,PasswordIterations,IsActive,CreatedAt)
                VALUES(@Id,@Username,@PasswordHash,@PasswordSalt,@PasswordIterations,@IsActive,SYSDATETIMEOFFSET());
                """, account, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new AdministratorConflictException();
        }
    }

    public async Task<AdministratorStateResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateSqlConnection();
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var currentState = await connection.QuerySingleOrDefaultAsync<bool?>(new CommandDefinition(
            "SELECT IsActive FROM dbo.PlatformAdministrators WITH (UPDLOCK, HOLDLOCK) WHERE Id=@Id;",
            new { Id = id }, transaction, cancellationToken: cancellationToken));
        if (currentState is null) { transaction.Rollback(); return AdministratorStateResult.NotFound; }
        if (!isActive && currentState.Value)
        {
            var activeCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM dbo.PlatformAdministrators WITH (UPDLOCK, HOLDLOCK) WHERE IsActive=1;",
                transaction: transaction, cancellationToken: cancellationToken));
            if (activeCount <= 1) { transaction.Rollback(); return AdministratorStateResult.LastActive; }
        }
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE dbo.PlatformAdministrators SET IsActive=@IsActive WHERE Id=@Id;",
            new { Id = id, IsActive = isActive }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return AdministratorStateResult.Updated;
    }

    public async Task<bool> ResetPasswordAsync(Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.PlatformAdministrators SET PasswordHash=@Hash,PasswordSalt=@Salt,PasswordIterations=@Iterations WHERE Id=@Id;
            """, new { Id = id, Hash = hash, Salt = salt, Iterations = iterations }, cancellationToken: cancellationToken)) == 1;
    }
}
