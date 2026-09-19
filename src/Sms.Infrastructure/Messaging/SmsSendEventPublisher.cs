using MassTransit;
using Sms.Application.Messages;

namespace Sms.Infrastructure.Messaging;

public sealed class SmsSendEventPublisher(IPublishEndpoint publishEndpoint) : ISmsSendEventPublisher
{
    public Task PublishAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default) =>
        publishEndpoint.Publish(new SmsSendEvent(Guid.NewGuid(), tenantId, messageId), cancellationToken);
}
