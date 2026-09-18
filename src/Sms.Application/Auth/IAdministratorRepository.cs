namespace Sms.Application.Auth;

public sealed record AdministratorAccount(Guid Id, string Username, string Email, byte[] PasswordHash,
    byte[] PasswordSalt, int PasswordIterations, bool IsActive);
public sealed record AdministratorSummary(Guid Id, string Username, string Email, bool IsActive, DateTimeOffset CreatedAt);
public enum AdministratorStateResult { Updated, NotFound, LastActive }
public sealed class AdministratorConflictException : Exception { }
public sealed class LastActiveAdministratorException : Exception { }

public interface IAdministratorRepository
{
    Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default);
    Task<AdministratorStateResult> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(Guid id, byte[] hash, byte[] salt, int iterations, CancellationToken cancellationToken = default);
}
