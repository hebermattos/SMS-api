using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Auth;
using Sms.Application.Auth;
using Sms.Application.Common;

namespace Sms.Api.Controllers;

public sealed record CreateTenantUserRequest(string Username, string Email, string Password, string Role);

[ApiController]
[Authorize(Policy = PortalSecurity.TenantAdministratorPolicy)]
[Route("api/v1/tenant/users")]
public sealed class TenantUsersController(
    ITenantContext tenantContext,
    TenantPortalUserManagementService users) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<PortalUserSummary>> List(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentException("Invalid pagination.");
        take = Math.Clamp(take, 1, 200);
        return (await users.ListAsync(tenantContext.TenantId, cancellationToken)).Skip(skip).Take(take).ToList();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var id = await users.CreateAsync(
            tenantContext.TenantId, request.Username, request.Email, request.Password,
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
