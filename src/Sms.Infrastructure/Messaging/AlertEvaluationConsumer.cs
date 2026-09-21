using Dapper;
using MassTransit;
using Sms.Application.Alerts;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertEvaluationConsumer(
    ReportingSqlConnectionFactory reportingConnectionFactory,
    IAlertRepository alerts) : IConsumer<AlertEvaluationEvent>
{
    public async Task Consume(ConsumeContext<AlertEvaluationEvent> context)
    {
        var message = context.Message;
        using var connection = reportingConnectionFactory.CreateConnection();

        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/AlertEvaluationConsumer.Consume.01.sql"),
            new { message.EventId, message.TenantId, message.Provider, message.Status, message.OccurredAtUtc },
            cancellationToken: context.CancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/AlertEvaluationConsumer.Consume.02.sql"),
            cancellationToken: context.CancellationToken));

        await alerts.EvaluateAsync(
            message.EventId, message.TenantId, message.Status, message.Provider, message.OccurredAtUtc, context.CancellationToken);
    }
}
