using Microsoft.Extensions.Configuration;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.Seed;
using Sms.Application.Auth;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var administrators = new AdministratorRepository(new SqlConnectionFactory(configuration));
var username = configuration["Admin:Username"] ?? throw new InvalidOperationException("Admin:Username is required for local bootstrap.");
var password = configuration["Admin:Password"] ?? throw new InvalidOperationException("Admin:Password is required for local bootstrap.");
if (await administrators.GetByUsernameAsync(username.Trim()) is null)
    await new AdministratorAuthenticationService(administrators).CreateAsync(username, password);
Console.WriteLine("Initial platform administrator is configured. Existing passwords are not overwritten.");
var providers = new TenantSmsProviderRepository(
    new SqlConnectionFactory(configuration), new AesGcmSecretProtector(configuration));
await ExampleProviders.SeedAsync(providers, Guid.Parse("11111111-1111-4111-8111-111111111111"));
Console.WriteLine("Example Twilio and Bandwidth providers configured with fictional credentials. Real SMS delivery is unavailable until valid account credentials are configured.");
