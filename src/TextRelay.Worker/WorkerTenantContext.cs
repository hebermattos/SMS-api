using Sms.Application.Common;

namespace Sms.Worker;

public sealed class WorkerTenantContext : IWorkerTenantContext
{
    private Guid? tenantId;

    public Guid TenantId => tenantId
        ?? throw new InvalidOperationException("Worker tenant context has not been initialized.");

    public void SetTenant(Guid tenantId) => this.tenantId = tenantId;
}
