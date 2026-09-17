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

    public async Task<IReadOnlyList<SmsStatusHistory>> GetStatusHistoryAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT Id, MessageId, Status, CreatedAt
            FROM dbo.SmsMessageStatusHistory
            WHERE TenantId = @TenantId AND MessageId = @MessageId
            ORDER BY CreatedAt, Id;
            """;
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SmsStatusHistory>(new CommandDefinition(sql, new { TenantId = tenantId, MessageId = messageId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task InsertAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            INSERT INTO dbo.SmsMessages (Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt)
            VALUES (@Id, @TenantId, @From, @To, @Body, @Provider, @ProviderMessageId, @Direction, @Status, @CreatedAt, @UpdatedAt);

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            VALUES (NEWID(), @TenantId, @Id, @Status, @CreatedAt);

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, message, cancellationToken: cancellationToken));
    }

    public async Task InsertInboundIfNotExistsAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @InsertedMessages TABLE (Id UNIQUEIDENTIFIER);

            IF NOT EXISTS (
                SELECT 1 FROM dbo.SmsMessages WITH (UPDLOCK, HOLDLOCK)
                WHERE TenantId = @TenantId AND Provider = @Provider AND ProviderMessageId = @ProviderMessageId
            )
            BEGIN
                INSERT INTO dbo.SmsMessages (Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id INTO @InsertedMessages
                VALUES (@Id, @TenantId, @From, @To, @Body, @Provider, @ProviderMessageId, @Direction, @Status, @CreatedAt, @UpdatedAt);
            END

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @CreatedAt FROM @InsertedMessages;

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, message, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid tenantId, Guid id, SmsStatus status, string? providerMessageId, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @ChangedMessages TABLE (Id UNIQUEIDENTIFIER, PreviousStatus INT);

            UPDATE dbo.SmsMessages
            SET Status = @Status, ProviderMessageId = COALESCE(@ProviderMessageId, ProviderMessageId), UpdatedAt = @UpdatedAt
            OUTPUT INSERTED.Id, DELETED.Status INTO @ChangedMessages
            WHERE TenantId = @TenantId AND Id = @Id;

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @UpdatedAt
            FROM @ChangedMessages
            WHERE PreviousStatus <> @Status;

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, Status = status, ProviderMessageId = providerMessageId, UpdatedAt = updatedAt }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusByProviderMessageIdAsync(Guid tenantId, string provider, string providerMessageId, SmsStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @ChangedMessages TABLE (Id UNIQUEIDENTIFIER, PreviousStatus INT);

            UPDATE dbo.SmsMessages
            SET Status = @Status, UpdatedAt = @UpdatedAt
            OUTPUT INSERTED.Id, DELETED.Status INTO @ChangedMessages
            WHERE TenantId = @TenantId AND Provider = @Provider AND ProviderMessageId = @ProviderMessageId
              AND (
                    Status = @Queued
                    OR (Status = @Sent AND @Status IN (@Delivered, @Failed))
                  );

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @UpdatedAt
            FROM @ChangedMessages
            WHERE PreviousStatus <> @Status;

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId, Provider = provider, ProviderMessageId = providerMessageId, Status = status, UpdatedAt = updatedAt,
            Queued = SmsStatus.Queued, Sent = SmsStatus.Sent, Delivered = SmsStatus.Delivered, Failed = SmsStatus.Failed
        }, cancellationToken: cancellationToken));
    }
}
