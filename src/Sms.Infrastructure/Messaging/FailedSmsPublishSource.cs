using Dapper;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public interface ISmsQueuePublishSource
{
    Task<IReadOnlyList<SmsQueuePublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default);
    Task MarkNotQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default);
}

public sealed record SmsQueuePublishMessage(Guid MessageId, Guid TenantId);

public sealed class SmsQueuePublishSource(SqlConnectionFactory connectionFactory) : ISmsQueuePublishSource
{
    public async Task<IReadOnlyList<SmsQueuePublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SmsQueuePublishMessage>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/SmsQueuePublishRetryWorker.PublishBatchAsync.01.sql"),
            new { NotQueued = SmsQueueStatus.NotQueued },
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<bool> TryMarkQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/SmsQueuePublishSource.MarkQueuedAsync.01.sql"),
            new { TenantId = tenantId, MessageId = messageId, Queued = SmsQueueStatus.Queued, NotQueued = SmsQueueStatus.NotQueued, UpdatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken));
        return affected == 1;
    }

    public async Task MarkNotQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/SmsQueuePublishSource.MarkNotQueuedAsync.01.sql"),
            new { TenantId = tenantId, MessageId = messageId, Queued = SmsQueueStatus.Queued, NotQueued = SmsQueueStatus.NotQueued, UpdatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken));
    }
}
