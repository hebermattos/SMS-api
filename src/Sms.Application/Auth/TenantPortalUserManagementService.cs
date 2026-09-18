using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Sms.Application.Auth;

public sealed class TenantPortalUserManagementService(
    ITenantPortalUserManagementRepository repository)
{
    public Task<IReadOnlyList<PortalUserSummary>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        repository.ListAsync(tenantId, cancellationToken);

    public async Task<Guid> CreateAsync(
        Guid tenantId, string username, string password, string role,
        CancellationToken cancellationToken = default)
    {
        ValidateUsername(username);
        ValidatePassword(password);
        ValidateRole(role);

        var (hash, salt) = HashPassword(password);
        return await repository.CreateAsync(new(
            Guid.NewGuid(), tenantId, username.Trim(), hash, salt, 600_000,
            "tenant", role), cancellationToken);
    }

    public async Task SetActiveAsync(
        Guid tenantId, Guid id, bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (!await repository.SetActiveAsync(tenantId, id, isActive, cancellationToken))
            throw new KeyNotFoundException();
    }

    public async Task ResetPasswordAsync(
        Guid tenantId, Guid id, string password,
        CancellationToken cancellationToken = default)
    {
        ValidatePassword(password);
        var (hash, salt) = HashPassword(password);
        if (!await repository.ResetPasswordAsync(tenantId, id, hash, salt, 600_000, cancellationToken))
            throw new KeyNotFoundException();
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)
            || !Regex.IsMatch(username.Trim(), @"A[a-zA-Z0-9][a-zA-Z0-9._-]{2,99}z"))
            throw new ArgumentException("Username must contain 3–100 letters, digits, dots, underscores, or hyphens.");
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 15 or > 128)
            throw new ArgumentException("Passwords must contain 15–128 characters.");
    }

    private static void ValidateRole(string role)
    {
        if (role is not ("user" or "administrator"))
            throw new ArgumentException("Role must be user or administrator.");
    }

    private static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        return (Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000,
            HashAlgorithmName.SHA256, 32), salt);
    }
}
