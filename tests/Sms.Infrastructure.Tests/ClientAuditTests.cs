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
