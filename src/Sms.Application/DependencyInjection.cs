using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Messages;
using Sms.Application.Tenants;

namespace Sms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SendSmsService>();
        services.AddScoped<ReceiveSmsWebhookService>();
        services.AddScoped<TenantProvisioningService>();
        return services;
    }
}
