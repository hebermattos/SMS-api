namespace Sms.Application.Auth;

public sealed record AdministratorAccount(Guid Id, string Username, byte[] PasswordHash,
    byte[] PasswordSalt, int PasswordIterations, bool IsActive);

public interface IAdministratorRepository
{
    Task<AdministratorAccount?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task CreateAsync(AdministratorAccount account, CancellationToken cancellationToken = default);
}
