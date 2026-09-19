using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class SmsSendConsumer(
    IServiceScopeFactory scopes,
    SqlConnectionFactory connectionFactory,
    IConfiguration configuration,
    ILogger<SmsSendConsumer> logger) : BackgroundService
{
    private readonly RabbitMqAlertOptions options = RabbitMqAlertOptions.From(configuration);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "SMS send consumer stopped; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Host, Port = options.Port, UserName = options.User,
            Password = options.Password, VirtualHost = options.VirtualHost,
            DispatchConsumersAsync = true
        };
        var rabbit = factory.CreateConnection();
        var channel = rabbit.CreateModel();
        channel.ExchangeDeclare(options.SendExchange, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.QueueDeclare(options.SendQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(options.SendQueue, options.SendExchange, options.SendRoutingKey);
        channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<SmsSendEvent>(args.Body.Span)
                    ?? throw new InvalidOperationException("Empty SMS send event.");
                await SendAsync(message, stoppingToken);
                channel.BasicAck(args.DeliveryTag, false);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Queued SMS processing failed; message will be retried.");
                channel.BasicNack(args.DeliveryTag, false, requeue: true);
            }
        };
        channel.BasicConsume(options.SendQueue, autoAck: false, consumer);
        return Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ContinueWith(_ =>
        {
            channel.Dispose();
            rabbit.Dispose();
        }, CancellationToken.None);
    }

    private async Task SendAsync(SmsSendEvent item, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var processed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.SmsSendInbox WHERE EventId=@EventId;",
            new { item.EventId }, cancellationToken: cancellationToken));
        if (processed != 0) return;

        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<IWorkerTenantContext>().SetTenant(item.TenantId);
        var repository = scope.ServiceProvider.GetRequiredService<ISmsMessageRepository>();
        var message = await repository.GetByIdAsync(item.TenantId, item.MessageId, cancellationToken)
            ?? throw new InvalidOperationException("Queued SMS message was not found.");
        if (message.Status != SmsStatus.Queued)
        {
            await MarkProcessedAsync(item.EventId, cancellationToken);
            return;
        }

        try
        {
            var provider = scope.ServiceProvider.GetRequiredService<ISmsProviderResolver>().Resolve(message.Provider);
            var result = await provider.SendAsync(message.From, message.To, message.Body, cancellationToken);
            await repository.UpdateStatusAsync(item.TenantId, item.MessageId, ParseStatus(result.Status),
                result.ProviderMessageId, DateTimeOffset.UtcNow, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SMS provider rejected queued message {MessageId}.", item.MessageId);
            await repository.UpdateStatusAsync(item.TenantId, item.MessageId, SmsStatus.Failed, null,
                DateTimeOffset.UtcNow, cancellationToken);
        }

        await MarkProcessedAsync(item.EventId, cancellationToken);
    }

    private async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT dbo.SmsSendInbox(EventId, ProcessedAtUtc)
            SELECT @EventId, SYSUTCDATETIME()
            WHERE NOT EXISTS (SELECT 1 FROM dbo.SmsSendInbox WHERE EventId=@EventId);
            """, new { EventId = eventId }, cancellationToken: cancellationToken));
    }

    private static SmsStatus ParseStatus(string status) => status.ToLowerInvariant() switch
    {
        "sent" => SmsStatus.Sent,
        "delivered" => SmsStatus.Delivered,
        "failed" => SmsStatus.Failed,
        _ => SmsStatus.Queued
    };
}
