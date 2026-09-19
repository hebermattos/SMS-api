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
        var rows = await connection.QueryAsync<SmsSendEvent>(new CommandDefinition("""
            WITH pending AS
            (
                SELECT TOP (100) *
                FROM dbo.SmsSendOutbox WITH (READPAST, UPDLOCK, ROWLOCK)
                WHERE PublishedAtUtc IS NULL
                  AND (LockedUntilUtc IS NULL OR LockedUntilUtc < SYSUTCDATETIME())
                ORDER BY CreatedAtUtc, Id
            )
            UPDATE pending
            SET LockId=@LockId, LockedUntilUtc=DATEADD(MINUTE,1,SYSUTCDATETIME()),
                AttemptCount=AttemptCount+1, LastAttemptAtUtc=SYSUTCDATETIME()
            OUTPUT INSERTED.Id AS EventId, INSERTED.TenantId, INSERTED.MessageId;
            """, new { LockId = lockId }, cancellationToken: cancellationToken));

        foreach (var row in rows)
        {
            await publishEndpoint.Publish(row, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE dbo.SmsSendOutbox
                SET PublishedAtUtc=SYSUTCDATETIME(), LockId=NULL, LockedUntilUtc=NULL
                WHERE Id=@EventId AND LockId=@LockId AND PublishedAtUtc IS NULL;
                """, new { row.EventId, LockId = lockId }, cancellationToken: cancellationToken));
        }
    }
}
