using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
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
using Sms.Application.Templates;
using Sms.Infrastructure.Caching;

namespace Sms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var cacheOptions = CacheOptions.From(configuration);
        services.AddSingleton(cacheOptions);

        if (cacheOptions.Enabled)
        {
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(redisConnectionString))
                throw new InvalidOperationException("Connection string 'Redis' is not configured when cache is enabled.");

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "sms-api:";
            });
        }
        else
        {
            services.AddSingleton<IDistributedCache, DisabledDistributedCache>();
        }

        services.AddSingleton<SqlConnectionFactory>();
        services.AddSingleton<LogsSqlConnectionFactory>();
        services.AddSingleton<ReportingSqlConnectionFactory>();
        services.AddSingleton<TenantConfigurationCache>();
        services.AddSingleton<IProviderCatalogCache, ProviderCatalogCache>();
        services.AddSingleton<ITenantSmsOverviewOutbox, TenantSmsOverviewOutbox>();
        services.AddSingleton<ITenantSmsOverviewEventPublisher, TenantSmsOverviewEventPublisher>();
        services.AddScoped<ITenantSmsOverviewProjection, TenantSmsOverviewProjection>();
        var rabbitMq = RabbitMqAlertOptions.From(configuration);
        services.AddSingleton(rabbitMq);
        services.AddHttpClient("RabbitMqManagement", client =>
        {
            client.BaseAddress = new Uri($"http://{rabbitMq.Host}:{rabbitMq.ManagementPort}/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{rabbitMq.User}:{rabbitMq.Password}")));
        });
        services.AddHostedService<RabbitMqMonitoringService>();
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
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPortalUserManagementRepository, PortalUserManagementRepository>();
        services.AddScoped<ITenantPortalUserManagementRepository, TenantPortalUserManagementRepository>();
        services.AddScoped<IAdministratorRepository, AdministratorRepository>();
        services.AddScoped<IAdministrationRepository, AdministrationRepository>();
        services.AddScoped<ITenantRateLimitRepository, TenantRateLimitRepository>();
        services.AddScoped<ITenantPortalRepository, TenantPortalRepository>();
        services.AddSingleton<IProviderSettingsPolicy, TwilioSettingsPolicy>();
        services.AddSingleton<IProviderSettingsPolicy, BandwidthSettingsPolicy>();
        services.AddSingleton<IProviderSettingsPolicy, MockSettingsPolicy>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantTimeZoneProvider, TenantTimeZoneProvider>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<ISmsMessageRepository, SmsMessageRepository>();
        services.AddScoped<IOptOutRepository, OptOutRepository>();
        services.AddScoped<IMessageTemplateRepository, MessageTemplateRepository>();
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
