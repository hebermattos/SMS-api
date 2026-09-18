using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sms.Api.Auth;
using Sms.Application.Auth;

namespace Sms.Api.Controllers;

public sealed record CreatePlatformUserRequest(string Username, string Email, string Password, string Role);
public sealed record PortalUserStateRequest(bool IsActive);
public sealed record PortalUserPasswordRequest(string Password);

[ApiController]
[Authorize(Policy = PortalSecurity.AdminPolicy)]
[Route("api/v1/admin/platform-users")]
public sealed class PlatformUsersController(PortalUserManagementService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PortalUserSummary>> List(CancellationToken cancellationToken) =>
        users.ListPlatformUsersAsync(cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(
        CreatePlatformUserRequest request,
        CancellationToken cancellationToken)
    {
        var id = await users.CreatePlatformUserAsync(
            request.Username, request.Email, request.Password, request.Role, cancellationToken);
        return Created($"/api/v1/admin/platform-users/{id}", new { id });
    }

    [HttpPut("{id:guid}/state")]
    public async Task<IActionResult> SetState(
        Guid id, PortalUserStateRequest request, CancellationToken cancellationToken)
    {
        await users.SetActiveAsync(id, request.IsActive, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id, PortalUserPasswordRequest request, CancellationToken cancellationToken)
    {
        await users.ResetPasswordAsync(id, request.Password, cancellationToken);
        return NoContent();
    }
}
