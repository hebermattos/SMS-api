using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
        var worker = new FailedSmsPublishRetryWorker(source, bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        var count = await worker.PublishBatchAsync();

        Assert.Equal(2, count);
        Assert.Equal([first.MessageId, second.MessageId], published.Select(x => x.MessageId));
        Assert.Equal([first.TenantId, second.TenantId], published.Select(x => x.TenantId));
        Assert.All(published, item => Assert.NotEqual(Guid.Empty, item.EventId));
        Assert.Equal([first.MessageId, second.MessageId], source.Claimed.Select(x => x.MessageId));
        Assert.Empty(source.Released);
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
        var source = new Source([first, second]);
        var worker = new FailedSmsPublishRetryWorker(source, bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        var count = await worker.PublishBatchAsync();

        Assert.Equal(1, count);
        bus.Verify(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal([first.MessageId, second.MessageId], source.Claimed.Select(x => x.MessageId));
        Assert.Equal([first.MessageId], source.Released.Select(x => x.MessageId));
    }

    [Fact]
    public async Task PublishBatchAsync_SkipsMessageWhenAnotherWorkerAlreadyClaimedIt()
    {
        var message = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        var source = new Source([message]) { CanClaim = false };
        var bus = new Mock<IBus>();
        var worker = new FailedSmsPublishRetryWorker(source, bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        var count = await worker.PublishBatchAsync();

        Assert.Equal(0, count);
        bus.Verify(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var worker = new FailedSmsPublishRetryWorker(new Source([message]), bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.PublishBatchAsync(cancellation.Token));
    }

    [Fact]
    public async Task PublishBatchAsync_ReleasesClaimAndPropagatesCancellation()
    {
        var message = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        var source = new Source([message]);
        using var cancellation = new CancellationTokenSource();
        var bus = new Mock<IBus>();
        bus.Setup(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var worker = new FailedSmsPublishRetryWorker(source, bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => worker.PublishBatchAsync(cancellation.Token));

        Assert.Equal([message.MessageId], source.Released.Select(x => x.MessageId));
    }

    [Fact]
    public async Task PublishBatchAsync_PropagatesClaimFailureWithoutPublishing()
    {
        var message = new FailedSmsPublishMessage(Guid.NewGuid(), Guid.NewGuid());
        var source = new ThrowingClaimSource(message);
        var bus = new Mock<IBus>();
        var worker = new FailedSmsPublishRetryWorker(source, bus.Object, NullLogger<FailedSmsPublishRetryWorker>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.PublishBatchAsync());

        bus.Verify(x => x.Publish(It.IsAny<SmsSendEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackgroundWorker_HandlesSourceFailureUntilStopped()
    {
        var source = new ThrowingPendingSource();
        var worker = new FailedSmsPublishRetryWorker(
            source,
            Mock.Of<IBus>(),
            NullLogger<FailedSmsPublishRetryWorker>.Instance);

        await worker.StartAsync(default);
        await source.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(default);

        Assert.True(source.Calls >= 1);
    }

    [Fact]
    public async Task BackgroundWorker_StartsAndStopsWithNoPendingMessages()
    {
        var worker = new FailedSmsPublishRetryWorker(
            new Source([]),
            Mock.Of<IBus>(),
            NullLogger<FailedSmsPublishRetryWorker>.Instance);

        await worker.StartAsync(default);
        await Task.Delay(25);
        await worker.StopAsync(default);
    }

    private sealed class ThrowingClaimSource(FailedSmsPublishMessage message) : IFailedSmsPublishSource
    {
        public Task<IReadOnlyList<FailedSmsPublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FailedSmsPublishMessage>>([message]);

        public Task<bool> TryMarkQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("claim failed");

        public Task MarkNotQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ThrowingPendingSource : IFailedSmsPublishSource
    {
        public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }

        public Task<IReadOnlyList<FailedSmsPublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            Called.TrySetResult();
            throw new InvalidOperationException("database unavailable");
        }

        public Task<bool> TryMarkQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task MarkNotQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class Source(IReadOnlyList<FailedSmsPublishMessage> messages) : IFailedSmsPublishSource
    {
        public List<FailedSmsPublishMessage> Claimed { get; } = [];
        public List<FailedSmsPublishMessage> Released { get; } = [];
        public bool CanClaim { get; init; } = true;

        public Task<IReadOnlyList<FailedSmsPublishMessage>> GetPendingAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(messages);

        public Task<bool> TryMarkQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
        {
            if (!CanClaim)
                return Task.FromResult(false);

            Claimed.Add(new FailedSmsPublishMessage(messageId, tenantId));
            return Task.FromResult(true);
        }

        public Task MarkNotQueuedAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
        {
            Released.Add(new FailedSmsPublishMessage(messageId, tenantId));
            return Task.CompletedTask;
        }
    }
}
