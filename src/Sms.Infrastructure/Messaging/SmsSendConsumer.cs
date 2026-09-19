using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Messaging;

public sealed class SmsSendConsumer(
    IWorkerTenantContext tenantContext,
    ISmsMessageRepository repository,
    ISmsProviderResolver providerResolver,
    SqlConnectionFactory connectionFactory,
    ILogger<SmsSendConsumer> logger) : IConsumer<SmsSendEvent>
{
    public async Task Consume(ConsumeContext<SmsSendEvent> context)
    {
        var item = context.Message;
        using var connection = connectionFactory.CreateConnection();
        var processed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.SmsSendInbox WHERE EventId=@EventId;",
            new { item.EventId }, cancellationToken: context.CancellationToken));
        if (processed != 0) return;

        tenantContext.SetTenant(item.TenantId);
        var message = await repository.GetByIdAsync(item.TenantId, item.MessageId, context.CancellationToken)
            ?? throw new InvalidOperationException("Queued SMS message was not found.");
        if (message.Status != SmsStatus.Queued)
        {
            await MarkProcessedAsync(item.EventId, context.CancellationToken);
            return;
        }

        try
        {
            var provider = providerResolver.Resolve(message.Provider);
            var result = await provider.SendAsync(message.From, message.To, message.Body, context.CancellationToken);
            await repository.UpdateStatusAsync(item.TenantId, item.MessageId, ParseStatus(result.Status),
                result.ProviderMessageId, DateTimeOffset.UtcNow, context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SMS provider rejected queued message {MessageId}.", item.MessageId);
            await repository.UpdateStatusAsync(item.TenantId, item.MessageId, SmsStatus.Failed, null,
                DateTimeOffset.UtcNow, context.CancellationToken);
        }

        await MarkProcessedAsync(item.EventId, context.CancellationToken);
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
