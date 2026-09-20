using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sms.Application.Messages;
using Sms.Domain.Messages;
using Sms.Infrastructure.Messaging;

namespace Sms.Infrastructure.Tests;

public sealed class FailedSmsPublishRetryWorkerTests
{
    [Fact]
    public async Task PublishBatchAsync_PublishesEveryFailedMessage()
    {
        var first = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        var second = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        var source = new Source([first, second]);
        var bus = new Mock<IBus>();
        var published = new List<SmsSendEvent>();
        bus.Setup(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SmsSendEvent, CancellationToken>((item, _) => published.Add(item))
            .Returns(Task.CompletedTask);
        var repository = new Mock<ISmsMessageRepository>();
        var worker = new FailedSmsPublishRetryWorker(source, repository.Object, bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        var count = await worker.PublishBatchAsync();

        Assert.Equal(2, count);
        Assert.Equal([first.MessageId, second.MessageId], published.Select(x => x.MessageId));
        Assert.Equal([first.TenantId, second.TenantId], published.Select(x => x.TenantId));
        Assert.All(published, item => Assert.NotEqual(Guid.Empty, item.EventId));
        repository.Verify(x => x.UpdateStatusAsync(first.TenantId, first.MessageId, SmsStatus.Queued, null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.UpdateStatusAsync(second.TenantId, second.MessageId, SmsStatus.Queued, null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishBatchAsync_ContinuesWhenOnePublishFails()
    {
        var first = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        var second = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        var bus = new Mock<IBus>();
        bus.SetupSequence(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("RabbitMQ unavailable"))
            .Returns(Task.CompletedTask);
        var repository = new Mock<ISmsMessageRepository>();
        var worker = new FailedSmsPublishRetryWorker(new Source([first, second]), repository.Object, bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        var count = await worker.PublishBatchAsync();

        Assert.Equal(1, count);
        bus.Verify(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        repository.Verify(x => x.UpdateStatusAsync(first.TenantId, first.MessageId, It.IsAny<SmsStatus>(), It.IsAny<string?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.UpdateStatusAsync(second.TenantId, second.MessageId, SmsStatus.Queued, null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishBatchAsync_PropagatesRequestedCancellation()
    {
        var message = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var bus = new Mock<IBus>();
        bus.Setup(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var worker = new FailedSmsPublishRetryWorker(new Source([message]), Mock.Of<ISmsMessageRepository>(), bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.PublishBatchAsync(cancellation.Token));
    }

    [Fact]
    public async Task BackgroundWorker_StartsAndStopsWithNoPendingMessages()
    {
        var worker = new FailedSmsPublishRetryWorker(
            new Source([]),
            Mock.Of<ISmsMessageRepository>(),
            Mock.Of<IBus>(),
            NullLogger<FailedSmsPublishRetryWorker>.Instance);

        await worker.StartAsync(default);
        await Task.Delay(25);
        await worker.StopAsync(default);
    }

    private sealed class Source(IReadOnlyList<FailedSmsPublishMessage> messages) : IFailedSmsPublishSource
    {
        public Task<IReadOnlyList<FailedSmsPublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(messages);
    }
}
