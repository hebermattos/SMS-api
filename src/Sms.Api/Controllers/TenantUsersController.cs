using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Auth;
using Sms.Application.Auth;
using Sms.Application.Common;

namespace Sms.Api.Controllers;

public sealed record CreateTenantUserRequest(string Username, string Password, string Role);

[ApiController]
[Authorize(Policy = PortalSecurity.TenantAdministratorPolicy)]
[Route("api/v1/tenant/users")]
public sealed class TenantUsersController(
    ITenantContext tenantContext,
    TenantPortalUserManagementService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PortalUserSummary>> List(CancellationToken cancellationToken) =>
        users.ListAsync(tenantContext.TenantId, cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var id = await users.CreateAsync(
            tenantContext.TenantId, request.Username, request.Password,
            request.Role, cancellationToken);
        return Created($"/api/v1/tenant/users/{id}", new { id });
    }

    [HttpPut("{id:guid}/state")]
    public async Task<IActionResult> SetState(
        Guid id, PortalUserStateRequest request, CancellationToken cancellationToken)
    {
        await users.SetActiveAsync(tenantContext.TenantId, id, request.IsActive, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id, PortalUserPasswordRequest request, CancellationToken cancellationToken)
    {
        await users.ResetPasswordAsync(tenantContext.TenantId, id, request.Password, cancellationToken);
        return NoContent();
    }
}
