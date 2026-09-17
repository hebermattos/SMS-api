using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Messages;

namespace Sms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SendSmsService>();
        return services;
    }
}
