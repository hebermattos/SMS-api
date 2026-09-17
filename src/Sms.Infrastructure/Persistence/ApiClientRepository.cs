using Dapper;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class ApiClientRepository(SqlConnectionFactory connectionFactory) : IApiClientRepository
{
    public async Task<ApiClientCredential?> GetActiveByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TenantId, ClientId, SecretHash, SecretSalt, SecretIterations
            FROM dbo.ApiClients
            WHERE ClientId = @ClientId AND IsActive = 1;
            """;
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApiClientCredential>(
            new CommandDefinition(sql, new { ClientId = clientId }, cancellationToken: cancellationToken));
    }

    public async Task CreateAsync(CreateApiClient client, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.ApiClients
                (Id, TenantId, ClientId, SecretHash, SecretSalt, SecretIterations, IsActive, CreatedAt)
            VALUES
                (@Id, @TenantId, @ClientId, @SecretHash, @SecretSalt, @SecretIterations, 1, @CreatedAt);
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(), client.TenantId, client.ClientId, client.SecretHash, client.SecretSalt,
            client.SecretIterations, CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken: cancellationToken));
    }
}
