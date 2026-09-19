using Dapper;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Sms.Application.Auth;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Tests;

[Collection(PostgresTestCollection.Name)]
public sealed class AdministratorSqlTests
{
    [PostgresFact]
    public async Task AccountsPersistHashedPasswordsAndEnforceUniqueCaseInsensitiveUsernames()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:Postgres"] = Environment.GetEnvironmentVariable("SMS_TEST_POSTGRES") }).Build();
        var factory = new SqlConnectionFactory(configuration);
        var repository = new AdministratorRepository(factory);
        var authentication = new AdministratorAuthenticationService(repository);
        var username = "admin-" + Guid.NewGuid().ToString("N");
        var id = await authentication.CreateAsync(username, "admin@example.com", "local-admin-test-password");
        using var connection = factory.CreateConnection();
        try
        {
            var account = await repository.GetByUsernameAsync(username.ToUpperInvariant());
            Assert.NotNull(account); Assert.Equal(id, account.Id);
            Assert.Equal(32, account.PasswordHash.Length); Assert.Equal(32, account.PasswordSalt.Length);
            Assert.True(await repository.IsActiveAsync(id));
            Assert.NotNull(await authentication.AuthenticateAsync(username.ToUpperInvariant(), "local-admin-test-password"));
            Assert.Null(await authentication.AuthenticateAsync(username, "wrong-password"));
            await Assert.ThrowsAsync<AdministratorConflictException>(() => authentication.CreateAsync(username.ToUpperInvariant(), "admin2@example.com", "another-local-password"));
            await connection.ExecuteAsync("UPDATE PlatformAdministrators SET IsActive=0 WHERE Id=@Id;", new { Id = id });
            Assert.False(await repository.IsActiveAsync(id));
            Assert.False(await repository.IsActiveAsync(Guid.NewGuid()));
            Assert.Null(await authentication.AuthenticateAsync(username, "local-admin-test-password"));
        }
        finally { await connection.ExecuteAsync("DELETE PlatformAdministrators WHERE Id=@Id;", new { Id = id }); }
    }
}
