using Dapper;
using Npgsql;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class AdministratorRepository(SqlConnectionFactory connections) : IAdministratorRepository
{
    public async Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AdministratorAccount>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.GetByUsernameAsync.04.sql"),
            new { Username = username }, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.IsActiveAsync.05.sql"),
            new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return (await connection.QueryAsync<AdministratorSummary>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.ListAsync.01.sql"), cancellationToken: cancellationToken))).AsList();
    }

    public async Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.CreateAsync.02.sql"), account, cancellationToken: cancellationToken));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
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
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.SetActiveAsync.06.sql"),
            new { Id = id }, transaction, cancellationToken: cancellationToken));
        if (currentState is null) { transaction.Rollback(); return AdministratorStateResult.NotFound; }
        if (!isActive && currentState.Value)
        {
            var activeCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.SetActiveAsync.07.sql"),
                transaction: transaction, cancellationToken: cancellationToken));
            if (activeCount <= 1) { transaction.Rollback(); return AdministratorStateResult.LastActive; }
        }
        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.SetActiveAsync.08.sql"),
            new { Id = id, IsActive = isActive }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return AdministratorStateResult.Updated;
    }

    public async Task<bool> ResetPasswordAsync(Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/AdministratorRepository.ResetPasswordAsync.03.sql"), new { Id = id, Hash = hash, Salt = salt, Iterations = iterations }, cancellationToken: cancellationToken)) == 1;
    }
}
