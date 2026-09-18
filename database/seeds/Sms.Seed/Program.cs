using Microsoft.Extensions.Configuration;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;
using Sms.Seed;

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var providers = new TenantSmsProviderRepository(
    new SqlConnectionFactory(configuration), new AesGcmSecretProtector(configuration));
await ExampleProviders.SeedAsync(providers, Guid.Parse("11111111-1111-4111-8111-111111111111"));
Console.WriteLine("Example Twilio and Bandwidth providers configured with fictional credentials. Real SMS delivery is unavailable until valid account credentials are configured.");
