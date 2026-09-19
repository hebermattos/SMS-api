using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Sms.Application.Alerts;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests;

[Collection(PostgresTestCollection.Name)]
public sealed class AlertSqlTests
{
    [PostgresFact]
    public async Task OnceRule_FiresOncePerIncidentAndPreservesTenantIsolation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = Environment.GetEnvironmentVariable("SMS_TEST_POSTGRES")
        }).Build();
        var repository = new AlertRepository(new SqlConnectionFactory(configuration));
        var tenantId = Guid.NewGuid(); var otherTenantId = Guid.NewGuid();
        var ruleId = Guid.NewGuid(); var message1 = Guid.NewGuid(); var message2 = Guid.NewGuid();
        using var connection = new NpgsqlConnection(configuration.GetConnectionString("Postgres"));
        await connection.OpenAsync();
        try
        {
            await connection.ExecuteAsync("""
                INSERT Tenants(Id,Name,IsActive,CreatedAt) VALUES
                    (@Tenant,'Alert tenant',TRUE,CURRENT_TIMESTAMP),(@Other,'Other tenant',TRUE,CURRENT_TIMESTAMP);
                INSERT SmsMessages(Id,TenantId,"From","To",Body,Provider,Direction,Status,CreatedAt) VALUES
                    (@Message1,@Tenant,'x','x','x','Twilio',1,4,CURRENT_TIMESTAMP),
                    (@Message2,@Tenant,'x','x','x','Twilio',1,4,CURRENT_TIMESTAMP);
                INSERT SmsMessageStatusHistory(Id,TenantId,MessageId,Status,CreatedAt) VALUES
                    (gen_random_uuid(),@Tenant,@Message1,4,CURRENT_TIMESTAMP),
                    (gen_random_uuid(),@Tenant,@Message2,4,CURRENT_TIMESTAMP);
                INSERT AlertStatusCounters(TenantId,Provider,Status,BucketStartUtc,MessageCount,UpdatedAtUtc)
                VALUES (@Tenant,'Twilio',4,date_trunc('minute', CURRENT_TIMESTAMP),2,CURRENT_TIMESTAMP);
                """, new { Tenant = tenantId, Other = otherTenantId, Message1 = message1, Message2 = message2 });

            await repository.CreateRuleAsync(new(ruleId, tenantId, "Failures", "Twilio", SmsStatus.Failed,
                2, 15, AlertRepeatMode.Once, null, true, false, null, DateTimeOffset.UtcNow, null));
            await repository.EvaluateAsync(tenantId);
            await repository.EvaluateAsync(tenantId);

            Assert.Single(await repository.ListAlertsAsync(tenantId, false, 0, 20));
            Assert.Empty(await repository.ListAlertsAsync(otherTenantId, false, 0, 20));

            await connection.ExecuteAsync("""
                UPDATE SmsMessageStatusHistory
                SET CreatedAt=CURRENT_TIMESTAMP - INTERVAL '1 hour'
                WHERE TenantId=@Tenant;
                UPDATE AlertStatusCounters
                SET BucketStartUtc=BucketStartUtc - INTERVAL '1 hour', UpdatedAtUtc=CURRENT_TIMESTAMP - INTERVAL '1 hour'
                WHERE TenantId=@Tenant;
                """, new { Tenant = tenantId });
            await repository.EvaluateAsync(tenantId);

            await connection.ExecuteAsync("""
                UPDATE SmsMessageStatusHistory
                SET CreatedAt=CURRENT_TIMESTAMP
                WHERE TenantId=@Tenant;
                UPDATE AlertStatusCounters
                SET BucketStartUtc=DATEADD(MINUTE,DATEDIFF(MINUTE,0,CAST(CURRENT_TIMESTAMP AS datetime2)),0) AT TIME ZONE 'UTC',
                    UpdatedAtUtc=CURRENT_TIMESTAMP
                WHERE TenantId=@Tenant;
                """, new { Tenant = tenantId });
            await repository.EvaluateAsync(tenantId);

            Assert.Equal(2, (await repository.ListAlertsAsync(tenantId, false, 0, 20)).Count);
        }
        finally
        {
            await connection.ExecuteAsync("""
                DELETE Alerts WHERE TenantId IN (@Tenant,@Other);
                DELETE AlertRules WHERE TenantId IN (@Tenant,@Other);
                DELETE SmsMessageStatusHistory WHERE TenantId IN (@Tenant,@Other);
                DELETE SmsMessages WHERE TenantId IN (@Tenant,@Other);
                DELETE Tenants WHERE Id IN (@Tenant,@Other);
                """, new { Tenant = tenantId, Other = otherTenantId });
        }
    }
}
