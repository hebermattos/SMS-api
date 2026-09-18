using Dapper;
using Microsoft.Data.SqlClient;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class PortalUserManagementRepository(SqlConnectionFactory connections)
    : IPortalUserManagementRepository
{
    public async Task<IReadOnlyList<PortalUserSummary>> ListPlatformUsersAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return (await connection.QueryAsync<PortalUserSummary>(new CommandDefinition("""
            SELECT Id, TenantId, Username, Context, Role, IsActive, CreatedAt
            FROM dbo.PortalUsers
            WHERE Context = 'platform'
            ORDER BY Username;
            """, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreatePlatformUserAsync(
        NewPortalUser user,
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT dbo.PortalUsers
                    (Id, TenantId, Username, PasswordHash, PasswordSalt, PasswordIterations,
                     Context, Role, IsActive, CreatedAt)
                VALUES
                    (@Id, NULL, @Username, @PasswordHash, @PasswordSalt, @PasswordIterations,
                     'platform', @Role, 1, SYSDATETIMEOFFSET());
                """, user, cancellationToken: cancellationToken));
            return user.Id;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new PortalUserConflictException();
        }
    }

    public async Task<bool> SetActiveAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.PortalUsers
            SET IsActive = @IsActive, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE Id = @Id AND Context = 'platform';
            """, new { Id = id, IsActive = isActive },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> ResetPasswordAsync(
        Guid id,
        byte[] hash,
        byte[] salt,
        int iterations,
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.PortalUsers
            SET PasswordHash = @Hash, PasswordSalt = @Salt,
                PasswordIterations = @Iterations, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE Id = @Id AND Context = 'platform';
            """, new { Id = id, Hash = hash, Salt = salt, Iterations = iterations },
            cancellationToken: cancellationToken)) == 1;
    }
}
