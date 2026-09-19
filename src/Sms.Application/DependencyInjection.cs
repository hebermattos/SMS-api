using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Messages;
using Sms.Application.Administration;
using Sms.Application.Tenants;
using Sms.Application.Auth;
using Sms.Application.Alerts;

namespace Sms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SendSmsService>();
        services.AddScoped<AdministratorAuthenticationService>();
        services.AddScoped<AdministrationService>();
        services.AddScoped<ReceiveSmsWebhookService>();
        services.AddScoped<TenantProvisioningService>();
        services.AddScoped<AlertService>();
        return services;
    }
}
