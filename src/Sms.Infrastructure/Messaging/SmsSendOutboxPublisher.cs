using Dapper;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class SmsSendOutboxPublisher(
    SqlConnectionFactory connectionFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<SmsSendOutboxPublisher> logger) : BackgroundService
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
                logger.LogError(exception, "Failed to publish SMS send outbox.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var lockId = Guid.NewGuid();
        var rows = await connection.QueryAsync<SmsSendEvent>(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/SmsSendOutboxPublisher.PublishBatchAsync.01.sql"), new { LockId = lockId }, cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            await publishEndpoint.Publish(row, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/SmsSendOutboxPublisher.PublishBatchAsync.02.sql"), new { row.EventId, LockId = lockId }, cancellationToken: cancellationToken));
        }
    }
}
