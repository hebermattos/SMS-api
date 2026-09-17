using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Messages;
using Sms.Application.Providers;
using Sms.Application.Security;
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
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();
        services.AddScoped<ITenantSmsProviderRepository, TenantSmsProviderRepository>();
        services.AddScoped<ISmsProviderResolver, SmsProviderResolver>();
        services.AddScoped<ISmsProvider, TwilioSmsProvider>();
        services.AddScoped<ISmsProvider, BandwidthSmsProvider>();
        return services;
    }
}
