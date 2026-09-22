using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sms.Infrastructure.Messaging;

public sealed class RabbitMqMonitoringService(
    IHttpClientFactory httpClientFactory,
    RabbitMqAlertOptions options,
    ILogger<RabbitMqMonitoringService> logger) : BackgroundService
{
    public const string MeterName = "Sms.Api.RabbitMq";
    private static readonly Meter Meter = new(MeterName);
    private static readonly UpDownCounter<long> Ready = Meter.CreateUpDownCounter<long>("rabbitmq.queue.messages.ready");
    private static readonly UpDownCounter<long> Unacknowledged = Meter.CreateUpDownCounter<long>("rabbitmq.queue.messages.unacknowledged");
    private static readonly UpDownCounter<long> Consumers = Meter.CreateUpDownCounter<long>("rabbitmq.queue.consumers");
    private readonly Dictionary<string, QueueMetrics> previous = new(StringComparer.Ordinal);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        await CollectAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CollectAsync(stoppingToken);
    }

    internal async Task CollectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("RabbitMqManagement");
            var virtualHost = Uri.EscapeDataString(options.VirtualHost);
            foreach (var queue in new[] { options.Queue, options.SendQueue, options.ReportingQueue })
            {
                var result = await client.GetFromJsonAsync<QueueMetrics>(
                    $"api/queues/{virtualHost}/{Uri.EscapeDataString(queue)}", cancellationToken);
                if (result is null) continue;

                var tags = new TagList { { "rabbitmq.queue", queue } };
                previous.TryGetValue(queue, out var old);
                Ready.Add(result.MessagesReady - (old?.MessagesReady ?? 0), tags);
                Unacknowledged.Add(result.MessagesUnacknowledged - (old?.MessagesUnacknowledged ?? 0), tags);
                Consumers.Add(result.Consumers - (old?.Consumers ?? 0), tags);
                previous[queue] = result;

                if (result.Consumers == 0)
                    logger.LogWarning("RabbitMQ queue {Queue}: {Ready} ready, {Unacknowledged} unacknowledged, {Consumers} consumers.",
                        queue, result.MessagesReady, result.MessagesUnacknowledged, result.Consumers);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Failed to collect RabbitMQ queue metrics.");
        }
    }

    private sealed record QueueMetrics(
        [property: System.Text.Json.Serialization.JsonPropertyName("messages_ready")] long MessagesReady,
        [property: System.Text.Json.Serialization.JsonPropertyName("messages_unacknowledged")] long MessagesUnacknowledged,
        [property: System.Text.Json.Serialization.JsonPropertyName("consumers")] long Consumers);
}
