using System.Net;
using Sms.Application.Common;
using Sms.Application.Providers;
using Sms.Infrastructure.Providers;

namespace Sms.Infrastructure.Tests;

public sealed class BandwidthSmsProviderTests
{
    [Fact]
    public async Task SendAsync_UsesOAuthBearerAndApplication()
    {
        var tenant=Guid.NewGuid();
        var messaging=new RecordingHandler(HttpStatusCode.Accepted, """{"id":"m1"}""");
        var oauth=new RecordingHandler(HttpStatusCode.OK, """{"access_token":"token"}""");
        var provider=Create(tenant,messaging,oauth,Config(tenant));
        var result=await provider.SendAsync("", "+15550000002", "hello");
        Assert.Equal("m1",result.ProviderMessageId);
        Assert.Equal("queued",result.Status);
        Assert.Equal("Bearer",messaging.Scheme);
        Assert.Contains("\"applicationId\":\"app-1\"",messaging.Body);
        Assert.Equal("Basic",oauth.Scheme);
        Assert.Contains("grant_type=client_credentials",oauth.Body);
    }

    [Fact]
    public async Task SendAsync_RejectsFromOverride()
    {
        var tenant=Guid.NewGuid();
        var provider=Create(tenant,new RecordingHandler(HttpStatusCode.Accepted,"{}"),new RecordingHandler(HttpStatusCode.OK,"{}"),Config(tenant));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>provider.SendAsync("+1999","+2","x"));
    }

    [Fact]
    public async Task SendAsync_RejectsMissingSettings()
    {
        var tenant=Guid.NewGuid();
        var config=new TenantSmsProviderConfiguration(tenant,"Bandwidth","client","secret","+1",true,true,null);
        var provider=Create(tenant,new RecordingHandler(HttpStatusCode.Accepted,"{}"),new RecordingHandler(HttpStatusCode.OK,"{}"),config);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>provider.SendAsync("","+2","x"));
    }

    private static BandwidthSmsProvider Create(Guid tenant,HttpMessageHandler messaging,HttpMessageHandler oauth,TenantSmsProviderConfiguration config)
    {
        var factory=new Factory(new HttpClient(oauth){BaseAddress=new Uri("https://api.bandwidth.com/")});
        return new BandwidthSmsProvider(new HttpClient(messaging){BaseAddress=new Uri("https://messaging.bandwidth.com/")},factory,new TenantContext(tenant),new Repo(config));
    }
    private static TenantSmsProviderConfiguration Config(Guid tenant)=>new(tenant,"Bandwidth","client-id","client-secret","+15550000001",true,true,"""{"accountId":"12345","applicationId":"app-1"}""");
    private sealed record TenantContext(Guid TenantId):ITenantContext;
    private sealed class Repo(TenantSmsProviderConfiguration c):ITenantSmsProviderRepository {
        public Task<TenantSmsProviderConfiguration?> GetAsync(Guid t,string p,CancellationToken x=default)=>Task.FromResult<TenantSmsProviderConfiguration?>(c);
        public Task<TenantSmsProviderConfiguration?> GetDefaultAsync(Guid t,CancellationToken x=default)=>Task.FromResult<TenantSmsProviderConfiguration?>(c);
        public Task<TenantSmsProviderConfiguration?> GetByAccountAndNumberAsync(string p,string a,string n,CancellationToken x=default)=>Task.FromResult<TenantSmsProviderConfiguration?>(c);
        public Task UpsertAsync(TenantSmsProviderConfiguration x,CancellationToken c=default)=>Task.CompletedTask;
    }
    private sealed class Factory(HttpClient client):IHttpClientFactory { public HttpClient CreateClient(string name)=>client; }
    private sealed class RecordingHandler(HttpStatusCode status,string response):HttpMessageHandler {
        public string Body{get;private set;}=""; public string? Scheme{get;private set;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){Body=r.Content is null?"":await r.Content.ReadAsStringAsync(c);Scheme=r.Headers.Authorization?.Scheme;return new HttpResponseMessage(status){Content=new StringContent(response)};}
    }
}
