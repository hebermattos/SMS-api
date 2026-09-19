using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Sms.Application.Alerts;
using Sms.Domain.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests;

[Collection(SqlServerTestCollection.Name)]
public sealed class AlertSqlTests
{
    [SqlServerFact]
    public async Task OnceRule_FiresOncePerIncidentAndPreservesTenantIsolation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] = Environment.GetEnvironmentVariable("SMS_TEST_SQLSERVER")
        }).Build();
        var repository = new AlertRepository(new SqlConnectionFactory(configuration));
        var tenantId = Guid.NewGuid(); var otherTenantId = Guid.NewGuid();
        var ruleId = Guid.NewGuid(); var message1 = Guid.NewGuid(); var message2 = Guid.NewGuid();
        using var connection = new SqlConnection(configuration.GetConnectionString("SqlServer"));
        await connection.OpenAsync();
        try
        {
            await connection.ExecuteAsync("""
                INSERT dbo.Tenants(Id,Name,IsActive,CreatedAt) VALUES
                    (@Tenant,N'Alert tenant',1,SYSUTCDATETIME()),(@Other,N'Other tenant',1,SYSUTCDATETIME());
                INSERT dbo.SmsMessages(Id,TenantId,[From],[To],Body,Provider,Direction,Status,CreatedAt) VALUES
                    (@Message1,@Tenant,N'x',N'x',N'x',N'Twilio',1,4,SYSUTCDATETIME()),
                    (@Message2,@Tenant,N'x',N'x',N'x',N'Twilio',1,4,SYSUTCDATETIME());
                INSERT dbo.SmsMessageStatusHistory(Id,TenantId,MessageId,Status,CreatedAt) VALUES
                    (NEWID(),@Tenant,@Message1,4,SYSUTCDATETIME()),
                    (NEWID(),@Tenant,@Message2,4,SYSUTCDATETIME());
                INSERT dbo.AlertStatusCounters(TenantId,Provider,Status,BucketStartUtc,MessageCount,UpdatedAtUtc)
                VALUES (@Tenant,N'Twilio',4,DATEADD(MINUTE,DATEDIFF(MINUTE,0,CAST(SYSUTCDATETIME() AS datetime2)),0) AT TIME ZONE 'UTC',2,SYSUTCDATETIME());
                """, new { Tenant = tenantId, Other = otherTenantId, Message1 = message1, Message2 = message2 });

            await repository.CreateRuleAsync(new(ruleId, tenantId, "Failures", "Twilio", SmsStatus.Failed,
                2, 15, AlertRepeatMode.Once, null, true, false, null, DateTimeOffset.UtcNow, null));
            await repository.EvaluateAsync();
            await repository.EvaluateAsync();

            Assert.Single(await repository.ListAlertsAsync(tenantId, false, 0, 20));
            Assert.Empty(await repository.ListAlertsAsync(otherTenantId, false, 0, 20));

            await connection.ExecuteAsync("""
                UPDATE dbo.SmsMessageStatusHistory
                SET CreatedAt=DATEADD(HOUR,-1,SYSUTCDATETIME())
                WHERE TenantId=@Tenant;
                UPDATE dbo.AlertStatusCounters
                SET BucketStartUtc=DATEADD(HOUR,-1,BucketStartUtc), UpdatedAtUtc=DATEADD(HOUR,-1,SYSUTCDATETIME())
                WHERE TenantId=@Tenant;
                """, new { Tenant = tenantId });
            await repository.EvaluateAsync();

            await connection.ExecuteAsync("""
                UPDATE dbo.SmsMessageStatusHistory
                SET CreatedAt=SYSUTCDATETIME()
                WHERE TenantId=@Tenant;
                UPDATE dbo.AlertStatusCounters
                SET BucketStartUtc=DATEADD(MINUTE,DATEDIFF(MINUTE,0,CAST(SYSUTCDATETIME() AS datetime2)),0) AT TIME ZONE 'UTC',
                    UpdatedAtUtc=SYSUTCDATETIME()
                WHERE TenantId=@Tenant;
                """, new { Tenant = tenantId });
            await repository.EvaluateAsync();

            Assert.Equal(2, (await repository.ListAlertsAsync(tenantId, false, 0, 20)).Count);
        }
        finally
        {
            await connection.ExecuteAsync("""
                DELETE dbo.Alerts WHERE TenantId IN (@Tenant,@Other);
                DELETE dbo.AlertRules WHERE TenantId IN (@Tenant,@Other);
                DELETE dbo.SmsMessageStatusHistory WHERE TenantId IN (@Tenant,@Other);
                DELETE dbo.SmsMessages WHERE TenantId IN (@Tenant,@Other);
                DELETE dbo.Tenants WHERE Id IN (@Tenant,@Other);
                """, new { Tenant = tenantId, Other = otherTenantId });
        }
    }
}
