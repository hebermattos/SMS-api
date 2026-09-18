using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Sms.Application.Messages;
using Sms.Application.Providers;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Providers;
using Sms.Infrastructure.Security;

namespace Sms.Infrastructure.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMS_TEST_SQLSERVER")))
            Skip = "Set SMS_TEST_SQLSERVER to a test database initialized with database/schema.sql.";
    }
}

public sealed class BandwidthWebhookSqlTests
{
    [SqlServerFact]
    public async Task Callbacks_PersistEncryptedSettingsAndIsolateIdempotentHistory()
    {
        var connectionString = Environment.GetEnvironmentVariable("SMS_TEST_SQLSERVER")!;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] = connectionString,
            ["Encryption:MasterKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        var factory = new SqlConnectionFactory(configuration);
        var providers = new TenantSmsProviderRepository(factory, new AesGcmSecretProtector(configuration));
        var messages = new SmsMessageRepository(factory);
        var service = new ReceiveSmsWebhookService(messages);
        var parser = new BandwidthWebhookParser(providers);
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var account = Guid.NewGuid().ToString("N");
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        try
        {
            await connection.ExecuteAsync("""
                INSERT dbo.Tenants (Id, Name, IsActive, CreatedAt)
                VALUES (@Tenant, N'Webhook test', 1, SYSDATETIMEOFFSET()),
                       (@OtherTenant, N'Other webhook test', 1, SYSDATETIMEOFFSET());
                """, new { Tenant = tenant, OtherTenant = otherTenant });
            var settings = "{\"accountId\":\"bandwidth-account\",\"applicationId\":\"app-1\",\"webhookPassword\":\"callback-password\"}";
            await providers.UpsertAsync(new(tenant, "Bandwidth", account, "oauth-secret", "+15550000001", true, true, settings));
            await providers.UpsertAsync(new(otherTenant, "Bandwidth", account, "other-oauth-secret", "+15550000003", true, true,
                settings.Replace("callback-password", "other-callback-password")));
            var stored = await connection.QuerySingleAsync<(string ApiSecret, string Settings)>(
                "SELECT ApiSecret, Settings FROM dbo.TenantSmsProviders WHERE TenantId=@Tenant AND Provider='Bandwidth';", new { Tenant = tenant });
            Assert.DoesNotContain("oauth-secret", stored.ApiSecret);
            Assert.DoesNotContain("callback-password", stored.Settings);
            Assert.DoesNotContain("applicationId", stored.Settings);
            Assert.Equal(settings, (await providers.GetAsync(tenant, "Bandwidth"))!.Settings);

            // Concurrent retries must create a single message and a single history entry.
            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Receive(BandwidthWebhooksControllerTests.Payload(), SmsDirection.Inbound)));
            var inbound = Assert.Single(await messages.GetHistoryAsync(tenant, 0, 100));
            Assert.Equal(SmsStatus.Received, inbound.Status);
            Assert.Single(await messages.GetStatusHistoryAsync(tenant, inbound.Id));
            Assert.Empty(await messages.GetHistoryAsync(otherTenant, 0, 100));
            Assert.Null(await messages.GetByIdAsync(otherTenant, inbound.Id));
            Assert.Empty(await messages.GetStatusHistoryAsync(otherTenant, inbound.Id));

            var outbound = Outbound(tenant, "outbound-1");
            var other = Outbound(otherTenant, "outbound-1");
            await messages.InsertAsync(outbound);
            await messages.InsertAsync(other);
            foreach (var type in new[] { "message-sent", "message-sent", "message-delivered", "message-delivered", "message-sending", "message-failed" })
            {
                var payload = BandwidthWebhooksControllerTests.Payload(type);
                payload[0]!["message"]!["id"] = "outbound-1";
                await Receive(payload, SmsDirection.Outbound);
            }
            Assert.Equal(SmsStatus.Delivered, (await messages.GetByIdAsync(tenant, outbound.Id))!.Status);
            Assert.Equal(new[] { SmsStatus.Queued, SmsStatus.Sent, SmsStatus.Delivered },
                (await messages.GetStatusHistoryAsync(tenant, outbound.Id)).Select(x => x.Status).OrderBy(x => x));
            Assert.Equal(SmsStatus.Queued, (await messages.GetByIdAsync(otherTenant, other.Id))!.Status);
            Assert.Single(await messages.GetStatusHistoryAsync(otherTenant, other.Id));

            var failed = Outbound(tenant, "outbound-failed");
            await messages.InsertAsync(failed);
            var failure = BandwidthWebhooksControllerTests.Payload("message-failed");
            failure[0]!["message"]!["id"] = "outbound-failed";
            await Receive(failure, SmsDirection.Outbound);
            await Receive(failure, SmsDirection.Outbound);
            Assert.Equal(SmsStatus.Failed, (await messages.GetByIdAsync(tenant, failed.Id))!.Status);
            Assert.Equal(2, (await messages.GetStatusHistoryAsync(tenant, failed.Id)).Count);

            // Shared OAuth account does not allow one tenant's callback password to access another number.
            var forged = BandwidthWebhooksControllerTests.Payload();
            forged[0]!["to"] = "+15550000003";
            forged[0]!["message"]!["owner"] = "+15550000003";
            forged[0]!["message"]!["to"] = new JsonArray("+15550000003");
            using var forgedBody = new MemoryStream(Encoding.UTF8.GetBytes(forged.ToJsonString()));
            Assert.Equal(SmsWebhookResult.Unauthorized, await service.ReceiveAsync(parser,
                BandwidthWebhooksControllerTests.Basic(account), forgedBody, SmsDirection.Inbound));
            Assert.Single(await messages.GetHistoryAsync(otherTenant, 0, 100));

            async Task Receive(JsonArray payload, SmsDirection direction)
            {
                using var body = new MemoryStream(Encoding.UTF8.GetBytes(payload.ToJsonString()));
                Assert.Equal(SmsWebhookResult.Accepted, await service.ReceiveAsync(parser,
                    BandwidthWebhooksControllerTests.Basic(account), body, direction));
            }
        }
        finally
        {
            await connection.ExecuteAsync("""
                DELETE dbo.SmsMessageStatusHistory WHERE TenantId IN (@Tenant, @OtherTenant);
                DELETE dbo.SmsMessages WHERE TenantId IN (@Tenant, @OtherTenant);
                DELETE dbo.TenantSmsProviders WHERE TenantId IN (@Tenant, @OtherTenant);
                DELETE dbo.Tenants WHERE Id IN (@Tenant, @OtherTenant);
                """, new { Tenant = tenant, OtherTenant = otherTenant });
        }
    }

    private static SmsMessage Outbound(Guid tenant, string providerId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant, Provider = "Bandwidth", ProviderMessageId = providerId,
        From = "+15550000001", To = "+15550000002", Body = "test", Direction = SmsDirection.Outbound,
        Status = SmsStatus.Queued, CreatedAt = DateTimeOffset.Parse("2026-09-17T11:00:00Z")
    };
}
