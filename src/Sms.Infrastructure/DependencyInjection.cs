using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Auth;
using Sms.Application.Messages;
using Sms.Application.Providers;
using Sms.Application.Security;
using Sms.Application.Tenants;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Providers;
using Sms.Infrastructure.Security;

namespace Sms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<TwilioWebhookValidator>();
        services.AddSingleton<ISmsWebhookUrlProvider, ConfiguredSmsWebhookUrlProvider>();
        services.AddScoped<IApiClientRepository, ApiClientRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();
        services.AddScoped<ITenantSmsProviderRepository, TenantSmsProviderRepository>();
        services.AddScoped<ISmsProviderResolver, SmsProviderResolver>();
        services.AddHttpClient<TwilioSmsProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.twilio.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ISmsProvider>(sp => sp.GetRequiredService<TwilioSmsProvider>());
        services.AddScoped<ISmsProvider, BandwidthSmsProvider>();
        return services;
    }
}
