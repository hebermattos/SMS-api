using Dapper;
using Sms.Application.Messages;
using Sms.Application.Security;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Persistence;

public sealed class SmsMessageRepository(SqlConnectionFactory connectionFactory, ISmsContentProtector protector) : ISmsMessageRepository
{
    public async Task<SmsMessage?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """SELECT Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt FROM dbo.SmsMessages WHERE TenantId = @TenantId AND Id = @Id;""";
        using var connection = connectionFactory.CreateConnection();
        var message = await connection.QuerySingleOrDefaultAsync<SmsMessage>(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken));
        return message is null ? null : Decrypt(message);
    }

    public async Task<IReadOnlyList<SmsMessage>> GetHistoryAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default)
    {
        const string sql = """SELECT Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt FROM dbo.SmsMessages WHERE TenantId = @TenantId ORDER BY CreatedAt DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;""";
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SmsMessage>(new CommandDefinition(sql, new { TenantId = tenantId, Skip = skip, Take = take }, cancellationToken: cancellationToken));
        return rows.Select(Decrypt).ToArray();
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
            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@CreatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE dbo.AlertStatusCounters WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = MessageCount + 1, UpdatedAtUtc = @CreatedAt
            WHERE TenantId = @TenantId AND Provider = @Provider AND Status = @Status AND BucketStartUtc = @BucketStartUtc;
            IF NOT EXISTS (SELECT 1 FROM dbo.AlertStatusCounters WHERE TenantId=@TenantId AND Provider=@Provider AND Status=@Status AND BucketStartUtc=@BucketStartUtc)
                INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
                VALUES (@TenantId, @Provider, @Status, @BucketStartUtc, 1, @CreatedAt);

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, EncryptedParameters(message), cancellationToken: cancellationToken));
    }

    public async Task InsertInboundIfNotExistsAsync(SmsMessage message, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @InsertedMessages TABLE (Id UNIQUEIDENTIFIER, Provider NVARCHAR(50));

            IF NOT EXISTS (
                SELECT 1 FROM dbo.SmsMessages WITH (UPDLOCK, HOLDLOCK)
                WHERE TenantId = @TenantId AND Provider = @Provider AND ProviderMessageId = @ProviderMessageId
            )
            BEGIN
                INSERT INTO dbo.SmsMessages (Id, TenantId, [From], [To], Body, Provider, ProviderMessageId, Direction, Status, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id, INSERTED.Provider INTO @InsertedMessages
                VALUES (@Id, @TenantId, @From, @To, @Body, @Provider, @ProviderMessageId, @Direction, @Status, @CreatedAt, @UpdatedAt);
            END

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @CreatedAt FROM @InsertedMessages;

            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@CreatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE c WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = MessageCount + 1, UpdatedAtUtc = @CreatedAt
            FROM dbo.AlertStatusCounters c
            INNER JOIN @InsertedMessages i ON i.Provider = c.Provider
            WHERE c.TenantId = @TenantId AND c.Status = @Status AND c.BucketStartUtc = @BucketStartUtc;
            INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
            SELECT @TenantId, i.Provider, @Status, @BucketStartUtc, COUNT(*), @CreatedAt
            FROM @InsertedMessages i
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertStatusCounters c WHERE c.TenantId=@TenantId AND c.Provider=i.Provider AND c.Status=@Status AND c.BucketStartUtc=@BucketStartUtc)
            GROUP BY i.Provider;

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, EncryptedParameters(message), cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid tenantId, Guid id, SmsStatus status, string? providerMessageId, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @ChangedMessages TABLE (Id UNIQUEIDENTIFIER, PreviousStatus INT, Provider NVARCHAR(50));

            UPDATE dbo.SmsMessages
            SET Status = @Status, ProviderMessageId = COALESCE(@ProviderMessageId, ProviderMessageId), UpdatedAt = @UpdatedAt
            OUTPUT INSERTED.Id, DELETED.Status, INSERTED.Provider INTO @ChangedMessages
            WHERE TenantId = @TenantId AND Id = @Id;

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @UpdatedAt
            FROM @ChangedMessages
            WHERE PreviousStatus <> @Status;

            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@UpdatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE c WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = c.MessageCount + x.Amount, UpdatedAtUtc = @UpdatedAt
            FROM dbo.AlertStatusCounters c
            INNER JOIN (SELECT Provider, COUNT(*) AS Amount FROM @ChangedMessages WHERE PreviousStatus <> @Status GROUP BY Provider) x
              ON x.Provider = c.Provider
            WHERE c.TenantId = @TenantId AND c.Status = @Status AND c.BucketStartUtc = @BucketStartUtc;
            INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
            SELECT @TenantId, x.Provider, @Status, @BucketStartUtc, x.Amount, @UpdatedAt
            FROM (SELECT Provider, COUNT(*) AS Amount FROM @ChangedMessages WHERE PreviousStatus <> @Status GROUP BY Provider) x
            WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertStatusCounters c WHERE c.TenantId=@TenantId AND c.Provider=x.Provider AND c.Status=@Status AND c.BucketStartUtc=@BucketStartUtc);

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

            DECLARE @ChangedMessages TABLE (Id UNIQUEIDENTIFIER, PreviousStatus INT, Provider NVARCHAR(50));

            UPDATE dbo.SmsMessages
            SET Status = @Status, UpdatedAt = @UpdatedAt
            OUTPUT INSERTED.Id, DELETED.Status, INSERTED.Provider INTO @ChangedMessages
            WHERE TenantId = @TenantId AND Provider = @Provider AND ProviderMessageId = @ProviderMessageId
              AND (
                    Status = @Queued
                    OR (Status = @Sent AND @Status IN (@Delivered, @Failed))
                  );

            INSERT INTO dbo.SmsMessageStatusHistory (Id, TenantId, MessageId, Status, CreatedAt)
            SELECT NEWID(), @TenantId, Id, @Status, @UpdatedAt
            FROM @ChangedMessages
            WHERE PreviousStatus <> @Status;

            DECLARE @BucketStartUtc DATETIMEOFFSET = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, CAST(@UpdatedAt AS datetime2)), 0) AT TIME ZONE 'UTC';
            UPDATE c WITH (UPDLOCK, SERIALIZABLE)
            SET MessageCount = c.MessageCount + x.Amount, UpdatedAtUtc = @UpdatedAt
            FROM dbo.AlertStatusCounters c
            INNER JOIN (SELECT Provider, COUNT(*) AS Amount FROM @ChangedMessages WHERE PreviousStatus <> @Status GROUP BY Provider) x
              ON x.Provider = c.Provider
            WHERE c.TenantId = @TenantId AND c.Status = @Status AND c.BucketStartUtc = @BucketStartUtc;
            IF @@ROWCOUNT = 0
                INSERT dbo.AlertStatusCounters(TenantId, Provider, Status, BucketStartUtc, MessageCount, UpdatedAtUtc)
                SELECT @TenantId, Provider, @Status, @BucketStartUtc, COUNT(*), @UpdatedAt
                FROM @ChangedMessages WHERE PreviousStatus <> @Status GROUP BY Provider;

            COMMIT TRANSACTION;
            """;
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId, Provider = provider, ProviderMessageId = providerMessageId, Status = status, UpdatedAt = updatedAt,
            Queued = SmsStatus.Queued, Sent = SmsStatus.Sent, Delivered = SmsStatus.Delivered, Failed = SmsStatus.Failed
        }, cancellationToken: cancellationToken));
    }

    private object EncryptedParameters(SmsMessage message) => new
    {
        message.Id,
        message.TenantId,
        From = protector.Protect(message.TenantId, message.Id, nameof(message.From), message.From),
        To = protector.Protect(message.TenantId, message.Id, nameof(message.To), message.To),
        Body = protector.Protect(message.TenantId, message.Id, nameof(message.Body), message.Body),
        message.Provider,
        message.ProviderMessageId,
        message.Direction,
        message.Status,
        message.CreatedAt,
        message.UpdatedAt
    };

    private SmsMessage Decrypt(SmsMessage message) => new()
    {
        Id = message.Id,
        TenantId = message.TenantId,
        From = protector.Unprotect(message.TenantId, message.Id, nameof(message.From), message.From),
        To = protector.Unprotect(message.TenantId, message.Id, nameof(message.To), message.To),
        Body = protector.Unprotect(message.TenantId, message.Id, nameof(message.Body), message.Body),
        Provider = message.Provider,
        ProviderMessageId = message.ProviderMessageId,
        Direction = message.Direction,
        Status = message.Status,
        CreatedAt = message.CreatedAt,
        UpdatedAt = message.UpdatedAt
    };
}
