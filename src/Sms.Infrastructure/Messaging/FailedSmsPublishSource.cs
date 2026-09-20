using Dapper;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public interface IFailedSmsPublishSource
{
    Task<IReadOnlyList<FailedSmsPublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default);
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
}
