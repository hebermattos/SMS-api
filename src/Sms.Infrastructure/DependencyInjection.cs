using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Messages;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Providers;

namespace Sms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SqlConnectionFactory>();
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();
        services.AddScoped<ISmsProviderResolver, SmsProviderResolver>();
        services.AddScoped<ISmsProvider, TwilioSmsProvider>();
        services.AddScoped<ISmsProvider, BandwidthSmsProvider>();
        return services;
    }
}
