using System.Security.Claims;
using Sms.Application.Common;

namespace Sms.Api.Auth;

public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid TenantId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue("tenant_id");
            return Guid.TryParse(value, out var tenantId)
                ? tenantId
                : throw new UnauthorizedAccessException("A valid tenant_id claim is required.");
        }
    }
}
