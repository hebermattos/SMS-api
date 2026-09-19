using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sms.Api.Auth;
using Sms.Api.Controllers;
using Sms.Api.Middleware;
using Sms.Application.Auth;

namespace Sms.Infrastructure.Tests;

public sealed class ClientAuditTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginUsesOnlyVerifiedIdentity(bool valid)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(
            new ControllerActionDescriptor { ControllerName = "Auth", ActionName = "Token" }), "login"));
        var hashed = ClientSecretHasher.Hash("correct-secret");
        var credential = new ApiClientCredential(Guid.NewGuid(), "verified-client", hashed.Hash, hashed.Salt, hashed.Iterations);
        var controller = new AuthController(new TokenService(Options.Create(new JwtOptions
            { Issuer = "i", Audience = "a", Key = "01234567890123456789012345678901" })), new Repository(credential))
            { ControllerContext = new ControllerContext { HttpContext = context } };
        var logger = new Recorder<ClientLoginAuditMiddleware>();
        await new ClientLoginAuditMiddleware(async c =>
        {
            var result = await controller.Token(new TokenRequest("untrusted-input", valid ? "correct-secret" : "wrong-secret"), default);
            c.Response.StatusCode = result is OkObjectResult ? 200 : 401;
        }, logger).InvokeAsync(context);
        Assert.Equal(valid ? "verified-client" : null, logger.Values["ClientId"]);
        Assert.Equal(valid ? credential.TenantId : (Guid?)null, logger.Values["TenantId"]);
        Assert.DoesNotContain("secret", logger.Message);
        Assert.DoesNotContain("untrusted-input", logger.Message);
        Assert.StartsWith(valid ? "Signed in to the API." : "Could not sign in to the API.", logger.Message);
    }

    [Theory]
    [InlineData("sub")]
    [InlineData(ClaimTypes.NameIdentifier)]
    public async Task HttpAuditUsesValidatedSubject(string subjectClaim)
    {
        var context = new DefaultHttpContext();
        var tenant = Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tenant_id", tenant.ToString()), new Claim(subjectClaim, "client-1")], "test"));
        context.Request.Headers["ClientId"] = "forged-client";
        var logger = new Recorder<RequestAuditMiddleware>();
        await new RequestAuditMiddleware(_ => Task.CompletedTask, logger).InvokeAsync(context);
        Assert.Equal("client-1", logger.Values["ClientId"]);
        Assert.Equal(tenant, logger.Values["TenantId"]);
        Assert.DoesNotContain("forged-client", logger.Message);
    }


    [Theory]
    [InlineData("Messages", "Send", 202, "Sent an SMS message.")]
    [InlineData("Messages", "GetById", 200, "Viewed an SMS message.")]
    [InlineData("Messages", "GetStatusHistory", 200, "Viewed SMS delivery history.")]
    [InlineData("Messages", "GetHistory", 200, "Viewed SMS history.")]
    [InlineData("Logs", "Get", 200, "Viewed activity logs.")]
    [InlineData("Overview", "Get", 200, "Viewed the account overview.")]
    [InlineData("Reports", "Sms", 200, "Viewed the SMS report.")]
    [InlineData("TenantUsers", "List", 200, "Viewed tenant users.")]
    [InlineData("TenantUsers", "Create", 201, "Created a tenant user.")]
    [InlineData("TenantUsers", "SetState", 204, "Updated a tenant user's status.")]
    [InlineData("TenantUsers", "ResetPassword", 204, "Reset a tenant user's password.")]
    [InlineData("Messages", "Send", 400, "Could not send an SMS message.")]
    public async Task HttpAuditUsesHumanReadableActivity(
        string controller, string action, int status, string expected)
    {
        var context = new DefaultHttpContext();
        var tenant = Guid.NewGuid();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("tenant_id", tenant.ToString()), new Claim("sub", "client-1")], "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(
            new ControllerActionDescriptor { ControllerName = controller, ActionName = action }), "activity"));
        var logger = new Recorder<RequestAuditMiddleware>();

        await new RequestAuditMiddleware(c =>
        {
            c.Response.StatusCode = status;
            return Task.CompletedTask;
        }, logger).InvokeAsync(context);

        Assert.StartsWith(expected, logger.Message);
        Assert.Equal(expected, logger.Values["Activity"]);
        Assert.Equal(status, logger.Values["StatusCode"]);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(429)]
    public async Task RejectedLoginHasNoTenant(int status)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(
            new ControllerActionDescriptor { ControllerName = "Auth", ActionName = "Token" }), "login"));
        var logger = new Recorder<ClientLoginAuditMiddleware>();
        await new ClientLoginAuditMiddleware(c => { c.Response.StatusCode = status; return Task.CompletedTask; }, logger).InvokeAsync(context);
        Assert.Null(logger.Values["TenantId"]);
        Assert.Null(logger.Values["ClientId"]);
        Assert.Equal("Failed", logger.Values["Outcome"]);
    }

    private sealed class Repository(ApiClientCredential credential) : IApiClientRepository
    {
        public Task<ApiClientCredential?> GetActiveByClientIdAsync(string clientId, CancellationToken cancellationToken = default) => Task.FromResult<ApiClientCredential?>(credential);
        public Task CreateAsync(CreateApiClient client, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Recorder<T> : ILogger<T>
    {
        public Dictionary<string, object?> Values { get; private set; } = [];
        public string Message { get; private set; } = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Values = ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(x => x.Key, x => x.Value);
            Message = formatter(state, exception);
        }
    }
}
