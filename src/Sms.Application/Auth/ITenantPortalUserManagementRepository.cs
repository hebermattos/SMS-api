namespace Sms.Application.Auth;

public interface ITenantPortalUserManagementRepository
{
    Task<IReadOnlyList<PortalUserSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(NewPortalUser user, CancellationToken cancellationToken = default);
    Task<bool> SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(Guid tenantId, Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default);
}
