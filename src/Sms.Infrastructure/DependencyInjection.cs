using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sms.Application.Auth;
using Sms.Application.Alerts;
using Sms.Application.Common;
using Sms.Application.Administration;
using Sms.Application.Messages;
using Sms.Application.Logs;
using Sms.Application.Providers;
using Sms.Application.Reports;
using Sms.Application.Security;
using Sms.Application.Tenants;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Providers;
using Sms.Infrastructure.Security;
using Sms.Infrastructure.Messaging;
using Sms.Application.OptOut;

namespace Sms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
            throw new InvalidOperationException("Connection string 'Redis' is not configured.");

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "sms-api:";
        });

        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<LogsSqlConnectionFactory>();
        services.AddSingleton<ReportingSqlConnectionFactory>();
        services.AddSingleton<ITenantSmsOverviewOutbox, TenantSmsOverviewOutbox>();
        services.AddSingleton<ITenantSmsOverviewEventPublisher, TenantSmsOverviewEventPublisher>();
        services.AddScoped<ITenantSmsOverviewProjection, TenantSmsOverviewProjection>();
        var rabbitMq = RabbitMqAlertOptions.From(configuration);
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<AlertEvaluationConsumer>();
            bus.AddConsumer<SmsSendConsumer>();
            bus.AddConsumer<TenantSmsOverviewConsumer>();
            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(rabbitMq.Host, (ushort)rabbitMq.Port, rabbitMq.VirtualHost, host =>
                {
                    host.Username(rabbitMq.User);
                    host.Password(rabbitMq.Password);
                });
                rabbit.ReceiveEndpoint(rabbitMq.Queue, endpoint =>
                {
                    endpoint.PrefetchCount = 1;
                    endpoint.ConcurrentMessageLimit = 1;
                    endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(5)));
                    endpoint.ConfigureConsumer<AlertEvaluationConsumer>(context);
                });
                rabbit.ReceiveEndpoint(rabbitMq.SendQueue, endpoint =>
                {
                    endpoint.PrefetchCount = 1;
                    endpoint.ConcurrentMessageLimit = 1;
                    endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(5)));
                    endpoint.ConfigureConsumer<SmsSendConsumer>(context);
                });
                rabbit.ReceiveEndpoint(rabbitMq.ReportingQueue, endpoint =>
                {
                    endpoint.PrefetchCount = 1;
                    endpoint.ConcurrentMessageLimit = 1;
                    endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(5)));
                    endpoint.ConfigureConsumer<TenantSmsOverviewConsumer>(context);
                });
            });
        });
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<ISmsContentProtector, AesGcmSmsContentProtector>();
        services.AddSingleton<TwilioWebhookValidator>();
        services.AddScoped<BandwidthWebhookParser>();
        services.AddSingleton<ISmsWebhookUrlProvider, ConfiguredSmsWebhookUrlProvider>();
        services.AddScoped<IApiClientRepository, ApiClientRepository>();
        services.AddScoped<IPortalUserRepository, PortalUserRepository>();
        services.AddScoped<IPortalUserManagementRepository, PortalUserManagementRepository>();
        services.AddScoped<ITenantPortalUserManagementRepository, TenantPortalUserManagementRepository>();
        services.AddScoped<IAdministratorRepository, AdministratorRepository>();
        services.AddScoped<IAdministrationRepository, AdministrationRepository>();
        services.AddScoped<ITenantPortalRepository, TenantPortalRepository>();
        services.AddSingleton<IProviderSettingsPolicy, TwilioSettingsPolicy>();
        services.AddSingleton<IProviderSettingsPolicy, BandwidthSettingsPolicy>();
        services.AddSingleton<IProviderSettingsPolicy, MockSettingsPolicy>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantTimeZoneProvider, TenantTimeZoneProvider>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();
        services.AddScoped<IOptOutRepository, OptOutRepository>();
        services.AddScoped<ISmsSendEventPublisher, SmsSendEventPublisher>();
        services.AddScoped<ISmsReportRepository, SmsReportRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<ILogEntryRepository, LogEntryRepository>();
        services.AddScoped<ITenantSmsProviderRepository, TenantSmsProviderRepository>();
        services.AddScoped<ISmsProviderResolver, SmsProviderResolver>();
        services.AddScoped<ISmsProvider, MockSmsProvider>();
        services.AddHttpClient<TwilioSmsProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.twilio.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ISmsProvider>(sp => sp.GetRequiredService<TwilioSmsProvider>());
        services.AddHttpClient<BandwidthSmsProvider>(client =>
        {
            client.BaseAddress = new Uri("https://messaging.bandwidth.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient("BandwidthOAuth", client =>
        {
            client.BaseAddress = new Uri("https://api.bandwidth.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ISmsProvider>(sp => sp.GetRequiredService<BandwidthSmsProvider>());
        return services;
    }
}
