using Dapper;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public interface IFailedSmsPublishSource
{
    Task<IReadOnlyList<FailedSmsPublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default);
    Task MarkNotQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default);
}

public sealed record FailedSmsPublishMessage(Guid MessageId, Guid TenantId);

public sealed class FailedSmsPublishSource(SqlConnectionFactory connectionFactory) : IFailedSmsPublishSource
{
    public async Task<IReadOnlyList<FailedSmsPublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<FailedSmsPublishMessage>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/FailedSmsPublishRetryWorker.PublishBatchAsync.01.sql"),
            new { NotQueued = SmsStatus.NotQueued },
            cancellationToken: cancellationToken));

        return rows.AsList();
    }

    public async Task<bool> TryMarkQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/FailedSmsPublishSource.MarkQueuedAsync.01.sql"),
            new { TenantId = tenantId, MessageId = messageId, Queued = SmsStatus.Queued, NotQueued = SmsStatus.NotQueued, UpdatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken));
        return affected == 1;
    }

    public async Task MarkNotQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/FailedSmsPublishSource.MarkNotQueuedAsync.01.sql"),
            new { TenantId = tenantId, MessageId = messageId, Queued = SmsStatus.Queued, NotQueued = SmsStatus.NotQueued, UpdatedAt = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken));
    }
}
