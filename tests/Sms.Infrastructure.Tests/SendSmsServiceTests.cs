using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Tests;

public sealed class SendSmsServiceTests
{
    [Fact]
    public async Task SendAsync_PersistsQueuedMessageWithoutCallingProvider()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeRepository();
        var provider = new FakeProvider("Twilio");
        var publisher = new FakePublisher();
        var service = new SendSmsService(new FakeTenantContext(tenantId), repository, new FakeResolver(provider), publisher, new(new TestOptOutRepository()));

        var result = await service.SendAsync(new SendSmsRequest(" +15551234567 ", "hello"));

        Assert.Equal("Twilio", result.Provider);
        Assert.Null(result.ProviderMessageId);
        Assert.Equal(nameof(SmsStatus.Queued), result.Status);
        Assert.NotNull(repository.Inserted);
        Assert.Equal(tenantId, repository.Inserted!.TenantId);
        Assert.Equal("+15551234567", repository.Inserted.To);
        Assert.Equal(SmsStatus.Queued, repository.Inserted.Status);
        Assert.Equal(0, provider.SendCalls);
        Assert.Equal((tenantId, repository.Inserted.Id), publisher.Published);
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData(" ", "body")]
    [InlineData("+1", "")]
    [InlineData("+1", " ")]
    public async Task SendAsync_RejectsInvalidRequest(string to, string body)
    {
        var service = new SendSmsService(
            new FakeTenantContext(Guid.NewGuid()), new FakeRepository(), new FakeResolver(new FakeProvider("Twilio")), new FakePublisher(), new(new TestOptOutRepository()));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(new SendSmsRequest(to, body)));
    }

    private sealed class FakePublisher : ISmsSendEventPublisher
    {
        public (Guid TenantId, Guid MessageId)? Published { get; private set; }

        public Task PublishAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default)
        {
            Published = (tenantId, messageId);
            return Task.CompletedTask;
        }
    }

    private sealed record FakeTenantContext(Guid TenantId) : ITenantContext;

    private sealed class FakeResolver(ISmsProvider provider) : ISmsProviderResolver
    {
        public ISmsProvider Resolve(string? providerName = null) => provider;
    }

    private sealed class FakeProvider(string name) : ISmsProvider
    {
        public string Name { get; } = name;
        public int SendCalls { get; private set; }

        public Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            return Task.FromResult(new ProviderSendResult("unexpected", "sent"));
        }
    }

    private sealed class FakeRepository : ISmsMessageRepository
    {
        public SmsMessage? Inserted { get; private set; }
        public Task InsertAsync(SmsMessage message, CancellationToken cancellationToken = default)
        {
            Inserted = message;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(Guid tenantId, Guid id, SmsStatus status, string? providerMessageId, DateTimeOffset updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SmsMessage?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SmsMessage?>(null);
        public Task<IReadOnlyList<SmsMessage>> GetHistoryAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SmsMessage>>([]);
        public Task<IReadOnlyList<SmsStatusHistory>> GetStatusHistoryAsync(Guid tenantId, Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SmsStatusHistory>>([]);
        public Task InsertInboundIfNotExistsAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusByProviderMessageIdAsync(Guid tenantId, string provider, string providerMessageId, SmsStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
