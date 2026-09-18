using Dapper;
using Sms.Application.Tenants;

namespace Sms.Infrastructure.Persistence;

public sealed class TenantRepository(SqlConnectionFactory connectionFactory) : ITenantRepository
{
    public async Task CreateAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO dbo.Tenants (Id, Name, TimeZoneId, IsActive, CreatedAt)
            VALUES (@Id, @Name, 'UTC', 1, @CreatedAt);
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Name = name, CreatedAt = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken));
    }
}
