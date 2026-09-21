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
    [InlineData(403)]
    [InlineData(500)]
    public async Task FailedPlatformOperationsAreSystemErrorsWithoutCustomerTenant(int status)
    {
        var context = Context("Administration", "SaveProvider");
        var tenant = Guid.NewGuid();
        context.Request.RouteValues["tenantId"] = tenant.ToString();
        var logger = new RecordingLogger();
        await RunPipelineAsync(context, c => { c.Response.StatusCode = status; return Task.CompletedTask; }, new PlatformAuditMiddleware(new PlatformActivities(), logger));
        Assert.Equal(LogLevel.Error, logger.Level);
        Assert.Equal("Administration.SaveProvider", logger.Values["Action"]);
        Assert.Equal(tenant, logger.Values["TargetTenantId"]);
        Assert.False(logger.Values.ContainsKey("TenantId"));
    }

    [Fact]
    public async Task SuccessfulPlatformOperationIsNotWrittenAsSystemLog()
    {
        var logger = new RecordingLogger();
        await RunPipelineAsync(Context("Administration", "SaveProvider"), c => { c.Response.StatusCode = 204; return Task.CompletedTask; }, new PlatformAuditMiddleware(new PlatformActivities(), logger));
        Assert.Empty(logger.Values);
    }

    [Fact]
    public async Task ExceptionIsRecordedAsFailureAndRethrownWithoutItsDetails()
    {
        var logger = new RecordingLogger();
        var step = new PlatformAuditMiddleware(new PlatformActivities(), logger);
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunPipelineAsync(Context("Administration", "UpdateTenant"), _ => throw new InvalidOperationException("secret"), step));
        Assert.Equal(500, logger.Values["StatusCode"]);
        Assert.DoesNotContain("secret", logger.Message);
    }

    [Theory]
    [InlineData("AdminAuth", 200)]
    [InlineData("AdminTenants", 201)]
    public async Task SuccessfulPlatformActionsAreNotTenantOrSystemLogs(string controller, int status)
    {
        var logger = new RecordingLogger();
        await RunPipelineAsync(Context(controller, "Token"), c => { c.Response.StatusCode = status; return Task.CompletedTask; }, new PlatformAuditMiddleware(new PlatformActivities(), logger));
        Assert.Empty(logger.Values);
    }

    [Fact]
    public async Task DoesNotAuditProviderCallbacksAsUserActions()
    {
        var logger = new RecordingLogger();
        await RunPipelineAsync(Context("TwilioWebhooks", "Inbound"), _ => Task.CompletedTask, new PlatformAuditMiddleware(new PlatformActivities(), logger));
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

    private sealed class PlatformActivities : IPlatformActivityWriter
    {
        public PlatformActivity? Last { get; private set; }
        public Task WriteAsync(PlatformActivity activity, CancellationToken cancellationToken = default)
        {
            Last = activity;
            return Task.CompletedTask;
        }
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
