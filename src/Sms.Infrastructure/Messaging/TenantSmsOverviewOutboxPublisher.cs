using Dapper;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class TenantSmsOverviewOutboxPublisher(
    SqlConnectionFactory connectionFactory,
    IPublishEndpoint publishEndpoint,
    ILogger<TenantSmsOverviewOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Failed to publish tenant SMS overview events."); }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var events = await connection.QueryAsync<TenantSmsOverviewEvent>(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/TenantSmsOverviewOutboxPublisher.PublishBatchAsync.01.sql"),
            cancellationToken: cancellationToken));

        foreach (var item in events)
        {
            await publishEndpoint.Publish(item, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/TenantSmsOverviewOutboxPublisher.PublishBatchAsync.02.sql"),
                new { item.EventId }, cancellationToken: cancellationToken));
        }
    }
}
