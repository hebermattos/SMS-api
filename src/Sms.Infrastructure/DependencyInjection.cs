using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Messages;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SqlConnectionFactory>();
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();
        return services;
    }
}
