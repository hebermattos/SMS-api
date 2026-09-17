namespace Sms.Application.Common;

public interface ITenantContext
{
    Guid TenantId { get; }
}
