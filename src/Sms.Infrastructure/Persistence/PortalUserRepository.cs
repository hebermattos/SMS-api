using Dapper;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class PortalUserRepository(SqlConnectionFactory connections) : IPortalUserRepository
{
    public async Task<PortalUserAccount?> GetActiveByUsernameAsync(
        string username,
        string context,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TenantId, Username, Email, PasswordHash, PasswordSalt, PasswordIterations,
                   Context, Role, IsActive
            FROM dbo.PortalUsers
            WHERE Username = @Username
              AND Context = @Context
              AND IsActive = 1
              AND (Context = 'platform' OR EXISTS
                  (SELECT 1 FROM dbo.Tenants t WHERE t.Id = TenantId AND t.IsActive = 1));
            """;

        using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PortalUserAccount>(
            new CommandDefinition(sql, new { Username = username, Context = context },
                cancellationToken: cancellationToken));
    }

    public async Task<PortalUserAccount?> GetActiveByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, TenantId, Username, PasswordHash, PasswordSalt, PasswordIterations,
                   Context, Role, IsActive
            FROM dbo.PortalUsers
            WHERE Id = @Id
              AND IsActive = 1
              AND (Context = 'platform' OR EXISTS
                  (SELECT 1 FROM dbo.Tenants t WHERE t.Id = TenantId AND t.IsActive = 1));
            """;

        using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<PortalUserAccount>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
