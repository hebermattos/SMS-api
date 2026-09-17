using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Tests;

public sealed class SendSmsServiceTests
{
    [Fact]
    public async Task SendAsync_PersistsAndUpdatesSuccessfulMessage()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeRepository();
        var service = new SendSmsService(new FakeTenantContext(tenantId), repository, new FakeResolver(new FakeProvider("Twilio", "SM123", "sent")));

        var result = await service.SendAsync(new SendSmsRequest(" +15551234567 ", "hello"));

        Assert.Equal("Twilio", result.Provider);
        Assert.Equal("SM123", result.ProviderMessageId);
        Assert.Equal(nameof(SmsStatus.Sent), result.Status);
        Assert.NotNull(repository.Inserted);
        Assert.Equal(tenantId, repository.Inserted!.TenantId);
        Assert.Equal("+15551234567", repository.Inserted.To);
        Assert.Equal(SmsStatus.Sent, repository.LastStatus);
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData(" ", "body")]
    [InlineData("+1", "")]
    [InlineData("+1", " ")]
    public async Task SendAsync_RejectsInvalidRequest(string to, string body)
    {
        var service = new SendSmsService(new FakeTenantContext(Guid.NewGuid()), new FakeRepository(), new FakeResolver(new FakeProvider("Twilio", "x", "sent")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(new SendSmsRequest(to, body)));
    }

    [Theory]
    [InlineData("delivered", SmsStatus.Delivered)]
    [InlineData("failed", SmsStatus.Failed)]
    [InlineData("queued", SmsStatus.Queued)]
    [InlineData("unknown", SmsStatus.Queued)]
    public async Task SendAsync_MapsProviderStatus(string providerStatus, SmsStatus expected)
    {
        var repository = new FakeRepository();
        var service = new SendSmsService(new FakeTenantContext(Guid.NewGuid()), repository, new FakeResolver(new FakeProvider("Twilio", "SM1", providerStatus)));
        await service.SendAsync(new SendSmsRequest("+1", "body"));
        Assert.Equal(expected, repository.LastStatus);
    }

    [Fact]
    public async Task SendAsync_MarksMessageFailedWhenProviderThrows()
    {
        var repository = new FakeRepository();
        var service = new SendSmsService(new FakeTenantContext(Guid.NewGuid()), repository, new FakeResolver(new ThrowingProvider()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendAsync(new SendSmsRequest("+1", "body")));
        Assert.Equal(SmsStatus.Failed, repository.LastStatus);
    }

    private sealed record FakeTenantContext(Guid TenantId) : ITenantContext;

    private sealed class FakeResolver(ISmsProvider provider) : ISmsProviderResolver
    {
        public ISmsProvider Resolve(string? providerName = null) => provider;
    }

    private sealed class FakeProvider(string name, string id, string status) : ISmsProvider
    {
        public string Name { get; } = name;
        public Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderSendResult(id, status));
    }

    private sealed class ThrowingProvider : ISmsProvider
    {
        public string Name => "Twilio";
        public Task<ProviderSendResult> SendAsync(string from, string to, string body, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("provider failure");
    }

    private sealed class FakeRepository : ISmsMessageRepository
    {
        public SmsMessage? Inserted { get; private set; }
        public SmsStatus? LastStatus { get; private set; }
        public Task InsertAsync(SmsMessage message, CancellationToken cancellationToken = default) { Inserted = message; return Task.CompletedTask; }
        public Task UpdateStatusAsync(Guid tenantId, Guid id, SmsStatus status, string? providerMessageId, DateTimeOffset updatedAt, CancellationToken cancellationToken = default) { LastStatus = status; return Task.CompletedTask; }
        public Task<SmsMessage?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SmsMessage?>(null);
        public Task<IReadOnlyList<SmsMessage>> GetHistoryAsync(Guid tenantId, int skip, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SmsMessage>>([]);
        public Task InsertInboundIfNotExistsAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateStatusByProviderMessageIdAsync(Guid tenantId, string provider, string providerMessageId, SmsStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
