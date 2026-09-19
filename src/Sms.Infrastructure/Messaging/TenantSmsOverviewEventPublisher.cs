using MassTransit;

namespace Sms.Infrastructure.Messaging;

public sealed class TenantSmsOverviewEventPublisher(IPublishEndpoint publishEndpoint) : ITenantSmsOverviewEventPublisher
{
    public Task PublishAsync(TenantSmsOverviewEvent item, CancellationToken cancellationToken = default) =>
        publishEndpoint.Publish(item, cancellationToken);
}
