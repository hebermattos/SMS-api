using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Sms.Application.OptOut;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;

namespace Sms.Infrastructure.Tests;

[Collection(SqlServerTestCollection.Name)]
public sealed class OptOutSqlTests
{
    [SqlServerFact]
    public async Task Repository_EncryptsNumbersAndEnforcesTenantIsolation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SqlServer"] = Environment.GetEnvironmentVariable("SMS_TEST_SQLSERVER"),
            ["Encryption:MasterKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        var factory = new SqlConnectionFactory(configuration);
        var repository = new OptOutRepository(factory, new AesGcmSmsContentProtector(configuration));
        var service = new OptOutService(repository);
        var tenant = Guid.NewGuid(); var other = Guid.NewGuid();
        using var connection = new SqlConnection(configuration.GetConnectionString("SqlServer"));
        await connection.OpenAsync();
        try
        {
            await connection.ExecuteAsync("INSERT dbo.Tenants(Id, Name, IsActive, CreatedAt) VALUES (@Tenant, N'Opt out test', 1, SYSDATETIMEOFFSET()), (@Other, N'Other opt out test', 1, SYSDATETIMEOFFSET());", new { Tenant = tenant, Other = other });
            await service.AddAsync(tenant, "+15551234567", "Customer request");

            Assert.True(await repository.IsBlockedAsync(tenant, "+15551234567"));
            Assert.False(await repository.IsBlockedAsync(other, "+15551234567"));
            Assert.Empty(await repository.ListAsync(other, 0, 20));
            var item = Assert.Single(await repository.ListAsync(tenant, 0, 20));
            Assert.Equal("+15551234567", item.PhoneNumber);

            var stored = await connection.QuerySingleAsync<string>("SELECT PhoneNumber FROM dbo.SmsOptOuts WHERE TenantId=@Tenant;", new { Tenant = tenant });
            Assert.DoesNotContain("15551234567", stored);
            Assert.False(await repository.RemoveAsync(other, item.Id));
            Assert.True(await repository.RemoveAsync(tenant, item.Id));
        }
        finally
        {
            await connection.ExecuteAsync("DELETE dbo.SmsOptOuts WHERE TenantId IN (@Tenant, @Other); DELETE dbo.Tenants WHERE Id IN (@Tenant, @Other);", new { Tenant = tenant, Other = other });
        }
    }
}
