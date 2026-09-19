using Dapper;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Persistence;

public sealed class ApiClientRepository(SqlConnectionFactory connectionFactory) : IApiClientRepository
{
    public async Task<ApiClientCredential?> GetActiveByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/ApiClientRepository.GetActiveByClientIdAsync.01.sql");
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ApiClientCredential>(
            new CommandDefinition(sql, new { ClientId = clientId }, cancellationToken: cancellationToken));
    }

    public async Task CreateAsync(CreateApiClient client, CancellationToken cancellationToken = default)
    {
        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Persistence/ApiClientRepository.CreateAsync.02.sql");
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(), client.TenantId, client.ClientId, client.SecretHash, client.SecretSalt,
            client.SecretIterations, CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken: cancellationToken));
    }
}
