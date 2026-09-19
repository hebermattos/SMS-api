using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Sms.Infrastructure.Messaging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ReportingSqlFactAttribute : FactAttribute
{
    public ReportingSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMS_TEST_SQLSERVER")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMS_TEST_REPORTING_SQLSERVER")))
            Skip = "Set SMS_TEST_SQLSERVER and SMS_TEST_REPORTING_SQLSERVER to databases initialized with the application and reporting schemas.";
    }
}

[Collection(SqlServerTestCollection.Name)]
public sealed class ReportingSqlTests
{
    [ReportingSqlFact]
    public async Task OverviewProjection_ConvergesWhenDeltasArriveOutOfOrder()
    {
        var applicationConnectionString = Environment.GetEnvironmentVariable("SMS_TEST_SQLSERVER");
        var reportingConnectionString = Environment.GetEnvironmentVariable("SMS_TEST_REPORTING_SQLSERVER");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReportingSqlServer"] = reportingConnectionString
        }).Build();
        var consumer = new TenantSmsOverviewConsumer(new TenantSmsOverviewProjection(new ReportingSqlConnectionFactory(configuration)));
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var eventIds = new List<Guid>();

        using var application = new SqlConnection(applicationConnectionString);
        using var reporting = new SqlConnection(reportingConnectionString);
        await application.OpenAsync();
        await reporting.OpenAsync();

        try
        {
            await application.ExecuteAsync("""
                INSERT dbo.Tenants(Id, Name, IsActive, CreatedAt)
                VALUES (@TenantId, N'Reporting tenant', 1, SYSUTCDATETIME());

                INSERT dbo.SmsMessages
                    (Id, TenantId, [From], [To], Body, Provider, Direction, Status, CreatedAt)
                VALUES
                    (@MessageId, @TenantId, N'encrypted-from', N'encrypted-to', N'encrypted-body', N'Mock', 1, 1, SYSUTCDATETIME());

                UPDATE dbo.SmsMessages
                SET Status = 3, UpdatedAt = SYSUTCDATETIME()
                WHERE TenantId = @TenantId AND Id = @MessageId;
                """, new { TenantId = tenantId, MessageId = messageId });

            var events = (await application.QueryAsync<TenantSmsOverviewEvent>("""
                SELECT EventId, TenantId, OutboundDelta, InboundDelta, DeliveredDelta,
                       FailedDelta, PendingDelta, OccurredAtUtc
                FROM dbo.TenantSmsOverviewOutbox
                WHERE TenantId = @TenantId
                ORDER BY SequenceNumber;
                """, new { TenantId = tenantId })).AsList();

            Assert.Equal(2, events.Count);
            eventIds.AddRange(events.Select(x => x.EventId));
            Assert.Equal((1L, 1L), (events[0].OutboundDelta, events[0].PendingDelta));
            Assert.Equal((1L, -1L), (events[1].DeliveredDelta, events[1].PendingDelta));

            await consumer.ApplyAsync(events[1]);
            await consumer.ApplyAsync(events[1]);
            await consumer.ApplyAsync(events[0]);

            var counters = await reporting.QuerySingleAsync<(long Outbound, long Inbound, long Delivered, long Failed, long Pending)>(
                "SELECT Outbound, Inbound, Delivered, Failed, Pending FROM dbo.TenantSmsOverview WHERE TenantId=@TenantId;",
                new { TenantId = tenantId });
            Assert.Equal((1L, 0L, 1L, 0L, 0L), counters);
        }
        finally
        {
            if (eventIds.Count > 0)
                await reporting.ExecuteAsync("DELETE dbo.TenantSmsOverviewInbox WHERE EventId IN @EventIds;", new { EventIds = eventIds });
            await reporting.ExecuteAsync("DELETE dbo.TenantSmsOverview WHERE TenantId=@TenantId;", new { TenantId = tenantId });
            await application.ExecuteAsync("""
                DELETE dbo.TenantSmsOverviewOutbox WHERE TenantId=@TenantId;
                DELETE dbo.SmsMessageStatusHistory WHERE TenantId=@TenantId;
                DELETE dbo.SmsMessages WHERE TenantId=@TenantId;
                DELETE dbo.Tenants WHERE Id=@TenantId;
                """, new { TenantId = tenantId });
        }
    }
}
