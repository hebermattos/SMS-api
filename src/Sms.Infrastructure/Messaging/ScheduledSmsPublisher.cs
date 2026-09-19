using Dapper;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class ScheduledSmsPublisher(
    SqlConnectionFactory connectionFactory,
    IBus bus,
    ILogger<ScheduledSmsPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to publish scheduled SMS messages.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    internal async Task PublishBatchAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ScheduledMessage>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/ScheduledSmsPublisher.PublishBatchAsync.01.sql"),
            cancellationToken: cancellationToken));

        foreach (var row in rows)
            await bus.Publish(new SmsSendEvent(row.MessageId, row.TenantId, row.MessageId), cancellationToken);
    }

    private sealed record ScheduledMessage(Guid MessageId, Guid TenantId);
}
