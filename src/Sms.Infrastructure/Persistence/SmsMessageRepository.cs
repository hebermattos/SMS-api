using Dapper;
using Sms.Application.Messages;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class SmsMessageRepository(SqlConnectionFactory connectionFactory) : ISmsMessageRepository
{
    public async Task<SmsMessage?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """SELECT Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt FROM dbo.SmsMessages WHERE TenantId = @TenantId AND Id = @Id;""";
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<SmsMessage>(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SmsMessage>> GetHistoryAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default)
    {
        const string sql = """SELECT Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt FROM dbo.SmsMessages WHERE TenantId = @TenantId ORDER BY CreatedAt DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;""";
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SmsMessage>(new CommandDefinition(sql, new { TenantId = tenantId, Skip = skip, Take = take }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task InsertAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        const string sql = """INSERT INTO dbo.SmsMessages (Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt) VALUES (@Id, @TenantId, @From, @To, @Body, @Provider, @ProviderMessageId, @Direction, @Status, @CreatedAt, @UpdatedAt);""";
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, message, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid tenantId, Guid id, SmsStatus status, string? providerMessageId, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """UPDATE dbo.SmsMessages SET Status = @Status, ProviderMessageId = COALESCE(@ProviderMessageId, ProviderMessageId), UpdatedAt = @UpdatedAt WHERE TenantId = @TenantId AND Id = @Id;""";
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, Status = status, ProviderMessageId = providerMessageId, UpdatedAt = updatedAt }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusByProviderMessageIdAsync(Guid tenantId, string provider, string providerMessageId, SmsStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """UPDATE dbo.SmsMessages SET Status = @Status, UpdatedAt = @UpdatedAt WHERE TenantId = @TenantId AND Provider = @Provider AND ProviderMessageId = @ProviderMessageId;""";
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Provider = provider, ProviderMessageId = providerMessageId, Status = status, UpdatedAt = updatedAt }, cancellationToken: cancellationToken));
    }
}
