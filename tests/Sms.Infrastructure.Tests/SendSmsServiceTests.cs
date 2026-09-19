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
        var service = CreateService(tenantId, repository, provider, publisher);

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
            new FakeTenantContext(Guid.NewGuid()), new FakeRepository(), new FakeResolver(new FakeProvider("Twilio")), new FakePublisher(),
            new(new TestOptOutRepository()), new FakeTimeZones(TimeZoneInfo.Utc), new FixedTimeProvider(DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(new SendSmsRequest(to, body)));
    }

    [Fact]
    public async Task SendAsync_ConvertsTenantLocalScheduleToUtcWithoutPublishingImmediately()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeRepository();
        var publisher = new FakePublisher();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var zone = TimeZoneInfo.CreateCustomTimeZone("Tenant/MinusThree", TimeSpan.FromHours(-3), "Tenant", "Tenant");
        var service = new SendSmsService(new FakeTenantContext(tenantId), repository, new FakeResolver(new FakeProvider("Twilio")),
            publisher, new(new TestOptOutRepository()), new FakeTimeZones(zone), clock);

        var result = await service.SendAsync(new SendSmsRequest("+15551234567", "hello", ScheduledAt: new DateTime(2026, 1, 1, 10, 0, 0)));

        Assert.Equal(nameof(SmsStatus.Scheduled), result.Status);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero), repository.Inserted!.ScheduledAtUtc);
        Assert.Null(publisher.Published);
    }

    [Fact]
    public async Task SendAsync_RejectsPastTenantLocalSchedule()
    {
        var service = CreateService(Guid.NewGuid(), new FakeRepository(), new FakeProvider("Twilio"), new FakePublisher());

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(
            new SendSmsRequest("+15551234567", "hello", ScheduledAt: new DateTime(2025, 12, 31, 23, 59, 0))));
    }

    private static SendSmsService CreateService(Guid tenantId, FakeRepository repository, FakeProvider provider, FakePublisher publisher) =>
        new(new FakeTenantContext(tenantId), repository, new FakeResolver(provider), publisher, new(new TestOptOutRepository()),
            new FakeTimeZones(TimeZoneInfo.Utc), new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

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
    private sealed record FakeTimeZones(TimeZoneInfo Zone) : ITenantTimeZoneProvider
    {
        public Task<TimeZoneInfo> GetAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(Zone);
    }
    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

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
        public Task<bool> TryQueueScheduledAsync(Guid tenantId, Guid id, DateTimeOffset updatedAt, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task UpdateStatusByProviderMessageIdAsync(Guid tenantId, string provider, string providerMessageId, SmsStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
