using Dapper;
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

    public async Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default)
    {
        using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT dbo.PlatformAdministrators(Id,Username,PasswordHash,PasswordSalt,PasswordIterations,IsActive,CreatedAt)
            VALUES(@Id,@Username,@PasswordHash,@PasswordSalt,@PasswordIterations,@IsActive,SYSDATETIMEOFFSET());
            """, account, cancellationToken: cancellationToken));
    }
}
