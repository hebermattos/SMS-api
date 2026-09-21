using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Sms.Api.Auth;
using Sms.Api.Controllers;
using Sms.Api.Middleware;
using Sms.Application.Common;

namespace Sms.Infrastructure.Tests;

public sealed class ActivityAuditTests
{
    [Fact]
    public async Task PageRecordsExplicitPageActivity()
    {
        var tenantId = Guid.NewGuid();
        var writer = new Recorder();
        var controller = new ActivityController(new TenantContext(tenantId), writer)
        {
            ControllerContext = new ControllerContext { HttpContext = Context("user-1") }
        };

        var result = await controller.Page(new PageActivityRequest("reports"));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(tenantId, writer.Activity!.TenantId);
        Assert.Equal("user-1", writer.Activity.UserId);
        Assert.Equal("PageView", writer.Activity.ActivityType);
        Assert.Equal("Opened Reports.", writer.Activity.Description);
    }

    [Fact]
    public async Task PageRejectsUnknownPageWithoutAudit()
    {
        var writer = new Recorder();
        var controller = new ActivityController(new TenantContext(Guid.NewGuid()), writer)
        {
            ControllerContext = new ControllerContext { HttpContext = Context("user-1") }
        };

        Assert.IsType<BadRequestObjectResult>(await controller.Page(new PageActivityRequest("unknown")));
        Assert.Null(writer.Activity);
    }

    [Theory]
    [InlineData(200, "Succeeded", "Signed in to the portal.")]
    [InlineData(401, "Failed", "Could not sign in to the portal.")]
    public async Task PortalLoginRecordsVerifiedTenantIdentity(int status, string outcome, string description)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var context = Context();
        context.Response.StatusCode = status;
        context.Items[PortalLoginAuditMiddleware.IdentityKey] =
            new PortalLoginIdentity(userId, tenantId, PortalSecurity.TenantContext);
        var writer = new Recorder();

        await new PortalLoginAuditMiddleware(writer).AuditAsync(new AuditPipelineContext(
            context,
            new ControllerActionDescriptor { ControllerName = "PortalAuth", ActionName = "Token" },
            false,
            0));

        Assert.Equal(tenantId, writer.Activity!.TenantId);
        Assert.Equal(userId.ToString(), writer.Activity.UserId);
        Assert.Equal(outcome, writer.Activity.Outcome);
        Assert.Equal(description, writer.Activity.Description);
    }

    [Fact]
    public async Task PortalLoginIgnoresUnverifiedIdentity()
    {
        var writer = new Recorder();
        await new PortalLoginAuditMiddleware(writer).AuditAsync(new AuditPipelineContext(
            Context(),
            new ControllerActionDescriptor { ControllerName = "PortalAuth", ActionName = "Token" },
            false,
            0));
        Assert.Null(writer.Activity);
    }

    private static DefaultHttpContext Context(string? subject = null)
    {
        var context = new DefaultHttpContext();
        if (subject is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "test"));
        return context;
    }

    private sealed class Recorder : IUserActivityWriter
    {
        public UserActivity? Activity { get; private set; }
        public Task WriteAsync(UserActivity activity, CancellationToken cancellationToken = default)
        {
            Activity = activity;
            return Task.CompletedTask;
        }
    }

    private sealed class TenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId => tenantId;
        public string TimeZoneId => "UTC";
    }
}
