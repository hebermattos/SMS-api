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
        return (await connection.QueryAsync<PortalUserSummary>(new CommandDefinition("""
            SELECT Id, TenantId, Username, Email, Context, Role, IsActive, CreatedAt
            FROM dbo.PortalUsers
            WHERE TenantId = @TenantId AND Context = 'tenant'
            ORDER BY Username;
            """, new { TenantId = tenantId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreateAsync(
        NewPortalUser user, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT dbo.PortalUsers
                    (Id, TenantId, Username, Email, PasswordHash, PasswordSalt, PasswordIterations,
                     Context, Role, IsActive, CreatedAt)
                VALUES
                    (@Id, @TenantId, @Username, @Email, @PasswordHash, @PasswordSalt, @PasswordIterations,
                     'tenant', @Role, 1, SYSDATETIMEOFFSET());
                """, user, cancellationToken: cancellationToken));
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
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.PortalUsers
            SET IsActive = @IsActive, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE TenantId = @TenantId AND Id = @Id AND Context = 'tenant';
            """, new { TenantId = tenantId, Id = id, IsActive = isActive },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> ResetPasswordAsync(
        Guid tenantId, Guid id, byte[] hash, byte[] salt, int iterations,
        CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.PortalUsers
            SET PasswordHash = @Hash, PasswordSalt = @Salt,
                PasswordIterations = @Iterations, UpdatedAt = SYSDATETIMEOFFSET()
            WHERE TenantId = @TenantId AND Id = @Id AND Context = 'tenant';
            """, new { TenantId = tenantId, Id = id, Hash = hash, Salt = salt, Iterations = iterations },
            cancellationToken: cancellationToken)) == 1;
    }
}
