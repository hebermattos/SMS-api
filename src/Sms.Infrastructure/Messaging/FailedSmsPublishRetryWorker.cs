using Dapper;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class FailedSmsPublishRetryWorker(
    SqlConnectionFactory connectionFactory,
    IBus bus,
    ILogger<FailedSmsPublishRetryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(5);

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
                logger.LogError(exception, "Failed to retry SMS queue publications.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    internal async Task<int> PublishBatchAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<FailedPublishMessage>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/FailedSmsPublishRetryWorker.PublishBatchAsync.01.sql"),
            new { PublishFailed = SmsStatus.PublishFailed },
            cancellationToken: cancellationToken));

        var published = 0;
        foreach (var row in rows)
        {
            try
            {
                await bus.Publish(
                    new SmsSendEvent(row.MessageId, row.TenantId, row.MessageId),
                    cancellationToken);
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to republish SMS message {MessageId}.", row.MessageId);
            }
        }

        return published;
    }

    private sealed record FailedPublishMessage(Guid MessageId, Guid TenantId);
}
