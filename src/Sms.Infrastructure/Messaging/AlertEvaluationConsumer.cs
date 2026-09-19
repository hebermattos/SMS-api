using Dapper;
using MassTransit;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertEvaluationConsumer(SqlConnectionFactory connectionFactory) : IConsumer<AlertEvaluationEvent>
{
    public async Task Consume(ConsumeContext<AlertEvaluationEvent> context)
    {
        var message = context.Message;
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);

        var inserted = await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/AlertEvaluationConsumer.Consume.01.sql"), new { message.EventId }, transaction, cancellationToken: context.CancellationToken));
        if (inserted == 0) { transaction.Commit(); return; }

        var sql = Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/AlertEvaluationConsumer.Consume.02.sql");
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = message.TenantId, Provider = message.Provider, Status = message.Status,
            OccurredAtUtc = message.OccurredAtUtc
        }, transaction, cancellationToken: context.CancellationToken));
        transaction.Commit();
    }
}
