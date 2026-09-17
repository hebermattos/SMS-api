namespace Sms.Application.Auth;

public sealed record ApiClientCredential(
    Guid TenantId,
    string ClientId,
    byte[] SecretHash,
    byte[] SecretSalt,
    int SecretIterations);

public interface IApiClientRepository
{
    Task<ApiClientCredential?> GetActiveByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
}
