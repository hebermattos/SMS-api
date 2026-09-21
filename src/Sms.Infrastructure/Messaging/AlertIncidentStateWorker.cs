using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class AlertIncidentStateWorker(
    SqlConnectionFactory connectionFactory,
    ILogger<AlertIncidentStateWorker> logger) : BackgroundService
{
    private static readonly TimeSpan EvaluationInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateTriggeredRulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to evaluate active alert incidents.");
            }

            await Task.Delay(EvaluationInterval, stoppingToken);
        }
    }

    private async Task EvaluateTriggeredRulesAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            Sms.Infrastructure.Sql.SqlQuery.Load("Messaging/AlertIncidentStateWorker.EvaluateTriggeredRulesAsync.01.sql"),
            cancellationToken: cancellationToken));
    }
}
