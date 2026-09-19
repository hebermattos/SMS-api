using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantPortalUserManagementRepository(SqlConnectionFactory connections)
    : ITenantPortalUserManagementRepository
{
    public async Task<IReadOnlyList<PortalUserSummary>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return (await connection.QueryAsync<PortalUserSummary>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantPortalUserManagementRepository.ListAsync.01.sql"), new { TenantId = tenantId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreateAsync(
        NewPortalUser user, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantPortalUserManagementRepository.CreateAsync.02.sql"), user, cancellationToken: cancellationToken));
            return user.Id;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new PortalUserConflictException();
        }
    }

    public async Task<bool> SetActiveAsync(
        Guid tenantId, Guid id, bool isActive,
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantPortalUserManagementRepository.SetActiveAsync.03.sql"), new { TenantId = tenantId, Id = id, IsActive = isActive },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> ResetPasswordAsync(
        Guid tenantId, Guid id, byte[] hash, byte[] salt, int iterations,
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/TenantPortalUserManagementRepository.ResetPasswordAsync.04.sql"), new { TenantId = tenantId, Id = id, Hash = hash, Salt = salt, Iterations = iterations },
            cancellationToken: cancellationToken)) == 1;
    }
}
