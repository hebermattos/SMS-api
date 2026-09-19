using Microsoft.AspNetCore.Mvc;
using Sms.Api.Controllers;
using Sms.Application.Common;
using Sms.Application.Messages;
using Sms.Domain.Messages;

namespace Sms.Infrastructure.Tests;

public sealed class MessagesControllerTests
{
    [Fact]
    public async Task GetById_UsesTenantAndReturnsMessage()
    {
        var tenantId=Guid.NewGuid(); var id=Guid.NewGuid();
        var message=new SmsMessage { Id=id, TenantId=tenantId, To="+1", Body="x" };
        var repo=new Repository(message);
        var controller=Create(tenantId,repo);
        Assert.IsType<OkObjectResult>(await controller.GetById(id,default));
        Assert.Equal(tenantId,repo.LastTenant);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound()
    {
        Assert.IsType<NotFoundResult>(await Create(Guid.NewGuid(),new Repository(null)).GetById(Guid.NewGuid(),default));
    }

    [Fact]
    public async Task GetStatusHistory_UsesAuthenticatedTenant()
    {
        var tenantId=Guid.NewGuid(); var id=Guid.NewGuid();
        var repo=new Repository(new SmsMessage { Id=id, TenantId=tenantId });

        Assert.IsType<OkObjectResult>(await Create(tenantId,repo).GetStatusHistory(id,default));
        Assert.Equal(tenantId,repo.LastTenant);
        Assert.Equal(id,repo.LastMessageId);
    }

    [Fact]
    public async Task GetStatusHistory_ReturnsNotFoundForMessageOutsideTenant()
    {
        var repo=new Repository(null);
        Assert.IsType<NotFoundResult>(await Create(Guid.NewGuid(),repo).GetStatusHistory(Guid.NewGuid(),default));
        Assert.Null(repo.LastMessageId);
    }

    [Fact]
    public async Task GetHistory_RejectsNegativeSkip()
    {
        Assert.IsType<BadRequestObjectResult>(await Create(Guid.NewGuid(),new Repository(null)).GetHistory(-1,50));
    }

    [Theory]
    [InlineData(0,1,1)]
    [InlineData(0,500,200)]
    [InlineData(0,0,1)]
    public async Task GetHistory_ClampsTake(int skip,int take,int expected)
    {
        var repo=new Repository(null);
        Assert.IsType<OkObjectResult>(await Create(Guid.NewGuid(),repo).GetHistory(skip,take));
        Assert.Equal(expected,repo.LastTake);
    }

    private static MessagesController Create(Guid tenantId, Repository repo)
    {
        var context=new TenantContext(tenantId);
        var service=new SendSmsService(context,repo,new Resolver(),new Publisher());
        return new MessagesController(context,repo,service);
    }
    private sealed record TenantContext(Guid TenantId):ITenantContext;
    private sealed class Resolver:ISmsProviderResolver { public ISmsProvider Resolve(string? provider=null)=>throw new NotSupportedException(); }
    private sealed class Publisher:ISmsSendEventPublisher { public Task PublishAsync(Guid tenantId,Guid messageId,CancellationToken cancellationToken=default)=>Task.CompletedTask; }
    private sealed class Repository(SmsMessage? message):ISmsMessageRepository
    {
        public Guid LastTenant{get;private set;} public Guid? LastMessageId{get;private set;} public int LastTake{get;private set;}
        public Task<SmsMessage?> GetByIdAsync(Guid tenantId,Guid id,CancellationToken cancellationToken=default){LastTenant=tenantId;return Task.FromResult(message);}
        public Task<IReadOnlyList<SmsMessage>> GetHistoryAsync(Guid tenantId,int skip,int take,CancellationToken cancellationToken=default){LastTenant=tenantId;LastTake=take;return Task.FromResult<IReadOnlyList<SmsMessage>>([]);}
        public Task<IReadOnlyList<SmsStatusHistory>> GetStatusHistoryAsync(Guid tenantId,Guid messageId,CancellationToken cancellationToken=default){LastTenant=tenantId;LastMessageId=messageId;return Task.FromResult<IReadOnlyList<SmsStatusHistory>>([]);}
        public Task InsertAsync(SmsMessage m,CancellationToken c=default)=>Task.CompletedTask;
        public Task InsertInboundIfNotExistsAsync(SmsMessage m,CancellationToken c=default)=>Task.CompletedTask;
        public Task UpdateStatusAsync(Guid t,Guid i,SmsStatus s,string? p,DateTimeOffset u,CancellationToken c=default)=>Task.CompletedTask;
        public Task UpdateStatusByProviderMessageIdAsync(Guid t,string p,string id,SmsStatus s,DateTimeOffset u,CancellationToken c=default)=>Task.CompletedTask;
    }
}
