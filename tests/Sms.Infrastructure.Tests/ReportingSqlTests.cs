using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Sms.Infrastructure.Messaging;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class ReportingSqlFactAttribute : FactAttribute
{
    public ReportingSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMS_TEST_POSTGRES")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMS_TEST_REPORTING_POSTGRES")))
            Skip = "Set SMS_TEST_POSTGRES and SMS_TEST_REPORTING_POSTGRES to PostgreSQL databases initialized with the application and reporting schemas.";
    }
}

[Collection(PostgresTestCollection.Name)]
public sealed class ReportingSqlTests
{
    [ReportingSqlFact]
    public async Task OverviewProjection_ConvergesWhenDeltasArriveOutOfOrder()
    {
        var applicationConnectionString = Environment.GetEnvironmentVariable("SMS_TEST_POSTGRES");
        var reportingConnectionString = Environment.GetEnvironmentVariable("SMS_TEST_REPORTING_POSTGRES");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ReportingPostgres"] = reportingConnectionString
        }).Build();
        var consumer = new TenantSmsOverviewConsumer(new TenantSmsOverviewProjection(new ReportingSqlConnectionFactory(configuration)));
        var tenantId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var eventIds = new List<Guid>();

        using var application = new NpgsqlConnection(applicationConnectionString);
        using var reporting = new NpgsqlConnection(reportingConnectionString);
        await application.OpenAsync();
        await reporting.OpenAsync();

        try
        {
            await application.ExecuteAsync("""
                INSERT Tenants(Id, Name, IsActive, CreatedAt)
                VALUES (@TenantId, 'Reporting tenant', TRUE, CURRENT_TIMESTAMP);

                INSERT PortalUsers(Id, TenantId, Username, Email, PasswordHash, PasswordSalt, PasswordIterations, Context, Role, IsActive, CreatedAt)
                VALUES (@UserId, @TenantId, 'report-user', 'report@example.com', decode('00','hex'), decode('00','hex'), 600000, 'tenant', 'user', TRUE, CURRENT_TIMESTAMP);

                INSERT SmsMessages
                    (Id, TenantId, UserId, "From", "To", Body, Provider, Direction, Status, CreatedAt)
                VALUES
                    (@MessageId, @TenantId, @UserId, 'encrypted-from', 'encrypted-to', 'encrypted-body', 'Mock', 1, 1, CURRENT_TIMESTAMP);

                UPDATE SmsMessages
                SET Status = 3, UpdatedAt = CURRENT_TIMESTAMP
                WHERE TenantId = @TenantId AND Id = @MessageId;
                """, new { TenantId = tenantId, MessageId = messageId, UserId = userId });

            var events = (await application.QueryAsync<TenantSmsOverviewEvent>("""
                SELECT EventId, TenantId, UserId, OutboundDelta, InboundDelta, DeliveredDelta,
                       FailedDelta, PendingDelta, OccurredAtUtc
                FROM TenantSmsOverviewOutbox
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
                "SELECT Outbound, Inbound, Delivered, Failed, Pending FROM TenantSmsOverview WHERE TenantId=@TenantId;",
                new { TenantId = tenantId });
            Assert.Equal((1L, 0L, 1L, 0L, 0L), counters);

            var userCounters = await reporting.QuerySingleAsync<(long TotalMessages, long Delivered, long Failed, long Pending)>(
                "SELECT TotalMessages, Delivered, Failed, Pending FROM UserSmsOverview WHERE TenantId=@TenantId AND UserId=@UserId AND ReportDate=CURRENT_DATE;",
                new { TenantId = tenantId, UserId = userId });
            Assert.Equal((1L, 1L, 0L, 0L), userCounters);
        }
        finally
        {
            if (eventIds.Count > 0)
                await reporting.ExecuteAsync("DELETE TenantSmsOverviewInbox WHERE EventId IN @EventIds;", new { EventIds = eventIds });
            await reporting.ExecuteAsync("DELETE UserSmsOverview WHERE TenantId=@TenantId; DELETE TenantSmsOverview WHERE TenantId=@TenantId;", new { TenantId = tenantId });
            await application.ExecuteAsync("""
                DELETE TenantSmsOverviewOutbox WHERE TenantId=@TenantId;
                DELETE SmsMessageStatusHistory WHERE TenantId=@TenantId;
                DELETE SmsMessages WHERE TenantId=@TenantId;
                DELETE PortalUsers WHERE TenantId=@TenantId;
                DELETE Tenants WHERE Id=@TenantId;
                """, new { TenantId = tenantId });
        }
    }
}
