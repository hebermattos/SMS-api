using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Sms.Api.Auth;
using Sms.Api.Middleware;

namespace Sms.Infrastructure.Tests;

public sealed class PlatformAuditMiddlewareTests
{
    [Fact]
    public async Task SuccessfulPlatformOperationIsPersistedOutsideSystemLogs()
    {
        var context = Context("Administration", "SaveProvider", authenticated: true);
        var tenant = Guid.NewGuid();
        context.Request.RouteValues["tenantId"] = tenant.ToString();
        var logger = new RecordingLogger();
        var activities = new ActivityRecorder();

        await RunPipelineAsync(context, c => { c.Response.StatusCode = 204; return Task.CompletedTask; },
            new PlatformAuditMiddleware(activities, logger));

        Assert.Empty(logger.Values);
        Assert.Equal("Administration.SaveProvider", activities.Activity!.Action);
        Assert.Equal("Tenant", activities.Activity.ResourceType);
        Assert.Equal(tenant.ToString(), activities.Activity.ResourceId);
        Assert.Equal("Succeeded", activities.Activity.Outcome);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(500)]
    public async Task FailedPlatformOperationsRemainSystemErrors(int status)
    {
        var context = Context("Administration", "SaveProvider", authenticated: true);
        var logger = new RecordingLogger();
        var activities = new ActivityRecorder();

        await RunPipelineAsync(context, c => { c.Response.StatusCode = status; return Task.CompletedTask; },
            new PlatformAuditMiddleware(activities, logger));

        Assert.Equal(LogLevel.Error, logger.Level);
        Assert.Equal("Administration.SaveProvider", logger.Values["Action"]);
        Assert.Equal("Failed", activities.Activity!.Outcome);
    }

    [Fact]
    public async Task ExceptionIsRecordedAsFailureAndRethrownWithoutItsDetails()
    {
        var logger = new RecordingLogger();
        var activities = new ActivityRecorder();
        var step = new PlatformAuditMiddleware(activities, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunPipelineAsync(Context("Administration", "UpdateTenant", authenticated: true),
                _ => throw new InvalidOperationException("secret"), step));

        Assert.Equal(500, logger.Values["StatusCode"]);
        Assert.Equal("Failed", activities.Activity!.Outcome);
        Assert.DoesNotContain("secret", logger.Message);
    }

    [Fact]
    public async Task SuccessfulAdminLoginUsesValidatedIdentity()
    {
        var context = Context("AdminAuth", "Token");
        context.Items[PortalSecurity.AdministratorLoginIdentityKey] = "admin-1";
        var activities = new ActivityRecorder();

        await RunPipelineAsync(context, c => { c.Response.StatusCode = 200; return Task.CompletedTask; },
            new PlatformAuditMiddleware(activities, new RecordingLogger()));

        Assert.Equal("admin-1", activities.Activity!.UserId);
        Assert.Equal("AdminAuth.Token", activities.Activity.Action);
    }

    [Fact]
    public async Task DoesNotAuditProviderCallbacksAsPlatformActions()
    {
        var activities = new ActivityRecorder();
        await RunPipelineAsync(Context("TwilioWebhooks", "Inbound"), _ => Task.CompletedTask,
            new PlatformAuditMiddleware(activities, new RecordingLogger()));
        Assert.Null(activities.Activity);
    }

    private static Task RunPipelineAsync(HttpContext context, RequestDelegate next, params IAuditPipelineStep[] steps) =>
        new AuditPipelineMiddleware(next).InvokeAsync(context, steps);

    private static DefaultHttpContext Context(string controller, string action, bool authenticated = false)
    {
        var context = new DefaultHttpContext();
        if (authenticated)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "admin-1"),
                new Claim(PortalSecurity.ContextClaim, PortalSecurity.PlatformContext),
                new Claim(PortalSecurity.RoleClaim, PortalSecurity.AdministratorRole)
            ], "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller, ActionName = action }), "test"));
        return context;
    }

    private sealed class ActivityRecorder : IPlatformActivityWriter
    {
        public PlatformActivity? Activity { get; private set; }
        public Task WriteAsync(PlatformActivity activity, CancellationToken cancellationToken = default)
        {
            Activity = activity;
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
