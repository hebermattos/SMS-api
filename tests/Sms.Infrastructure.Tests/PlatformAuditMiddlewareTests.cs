using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Sms.Api.Auth;
using Sms.Api.Middleware;

namespace Sms.Infrastructure.Tests;

public sealed class PlatformAuditMiddlewareTests
{
    [Theory]
    [InlineData(204, LogLevel.Information, "Succeeded")]
    [InlineData(403, LogLevel.Warning, "Failed")]
    [InlineData(500, LogLevel.Error, "Failed")]
    public async Task RecordsSafeActionMetadataWithoutCustomerTenant(int status, LogLevel level, string outcome)
    {
        var context = Context("Administration", "SaveProvider");
        var tenant = Guid.NewGuid();
        context.Request.RouteValues["tenantId"] = tenant.ToString();
        context.Request.RouteValues["provider"] = "secret-phone-number";
        context.Request.Headers.Authorization = "Bearer secret-token";
        var administratorId = Guid.NewGuid().ToString();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(PortalSecurity.ContextClaim, PortalSecurity.PlatformContext), new Claim(PortalSecurity.RoleClaim, PortalSecurity.AdministratorRole), new Claim(ClaimTypes.NameIdentifier, administratorId)], "test"));
        var logger = new RecordingLogger();
        await RunPipelineAsync(context, c => { c.Response.StatusCode = status; return Task.CompletedTask; }, new PlatformAuditMiddleware(logger));
        Assert.Equal(level, logger.Level);
        Assert.Equal("Administration.SaveProvider", logger.Values["Action"]);
        Assert.Equal(administratorId, logger.Values["Actor"]);
        Assert.Equal(outcome, logger.Values["Outcome"]);
        Assert.Equal(tenant, logger.Values["TargetTenantId"]);
        Assert.False(logger.Values.ContainsKey("TenantId"));
        Assert.DoesNotContain("secret", logger.Message);
    }

    [Fact]
    public async Task ExceptionIsRecordedAsFailureAndRethrownWithoutItsDetails()
    {
        var logger = new RecordingLogger();
        var step = new PlatformAuditMiddleware(logger);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunPipelineAsync(Context("Administration", "UpdateTenant"), _ => throw new InvalidOperationException("secret"), step));
        Assert.Equal(500, logger.Values["StatusCode"]);
        Assert.DoesNotContain("secret", logger.Message);
    }

    [Theory]
    [InlineData("AdminAuth", 200, "verified-admin-id")]
    [InlineData("AdminAuth", 401, "unauthenticated")]
    [InlineData("AdminAuth", 429, "unauthenticated")]
    [InlineData("AdminTenants", 201, "bootstrap-key")]
    public async Task IdentifiesLoginAndBootstrapOutcomes(string controller, int status, string actor)
    {
        var logger = new RecordingLogger();
        await RunPipelineAsync(Context(controller, "Token"), c => {
            c.Response.StatusCode = status;
            if (status == 200) c.Items[PortalSecurity.AdministratorLoginIdentityKey] = "verified-admin-id";
            return Task.CompletedTask;
        }, new PlatformAuditMiddleware(logger));
        Assert.Equal(actor, logger.Values["Actor"]);
    }

    [Fact]
    public async Task DoesNotAuditProviderCallbacksAsUserActions()
    {
        var logger = new RecordingLogger();
        await RunPipelineAsync(Context("TwilioWebhooks", "Inbound"), _ => Task.CompletedTask, new PlatformAuditMiddleware(logger));
        Assert.Empty(logger.Values);
    }

    private static Task RunPipelineAsync(HttpContext context, RequestDelegate next, params IAuditPipelineStep[] steps) =>
        new AuditPipelineMiddleware(next).InvokeAsync(context, steps);

    private static DefaultHttpContext Context(string controller, string action)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller, ActionName = action }), "test"));
        return context;
    }

    private sealed class RecordingLogger : ILogger<PlatformAuditMiddleware>
    {
        public Dictionary<string, object?> Values { get; private set; } = [];
        public LogLevel Level { get; private set; }
        public string Message { get; private set; } = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Level = level;
            Values = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(x => x.Key, x => x.Value);
            Message = formatter(state, exception);
        }
    }
}
