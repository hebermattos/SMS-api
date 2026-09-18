using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Net.Mail;

namespace Sms.Application.Auth;

public sealed class AdministratorAuthenticationService(IAdministratorRepository administrators)
{
    public const int PasswordIterations = 600_000;
    private static readonly byte[] DummyHash = new byte[32];
    private static readonly byte[] DummySalt = new byte[32];

    public async Task<AdministratorAccount?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100 || string.IsNullOrEmpty(password) || password.Length > 128)
            return null;
        var account = await administrators.GetByUsernameAsync(username.Trim(), cancellationToken);
        // Unknown and inactive accounts perform the same password work and return the same failure.
        var valid = ClientSecretHasher.Verify(password, account?.PasswordHash ?? DummyHash,
            account?.PasswordSalt ?? DummySalt, account?.PasswordIterations ?? PasswordIterations);
        return valid && account?.IsActive == true ? account : null;
    }

    public async Task<Guid> CreateAsync(string username, string email, string password, CancellationToken cancellationToken = default)
    {
        ValidateUsername(username);
        ValidateEmail(email);
        var (hash, salt) = HashPassword(password);
        var id = Guid.NewGuid();
        await administrators.CreateAsync(new(id, username.Trim(), email.Trim().ToLowerInvariant(), hash, salt, PasswordIterations, true), cancellationToken);
        return id;
    }

    public Task<IReadOnlyList<AdministratorSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        administrators.ListAsync(cancellationToken);

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var result = await administrators.SetActiveAsync(id, isActive, cancellationToken);
        if (result == AdministratorStateResult.NotFound) throw new KeyNotFoundException();
        if (result == AdministratorStateResult.LastActive)
            throw new LastActiveAdministratorException();
    }

    public async Task ResetPasswordAsync(Guid id, string password, CancellationToken cancellationToken = default)
    {
        var (hash, salt) = HashPassword(password);
        if (!await administrators.ResetPasswordAsync(id, hash, salt, PasswordIterations, cancellationToken))
            throw new KeyNotFoundException();
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username) || !Regex.IsMatch(username.Trim(), @"\A[a-zA-Z0-9][a-zA-Z0-9._-]{2,99}\z"))
            throw new ArgumentException("Username must contain 3–100 letters, digits, dots, underscores, or hyphens and start with a letter or digit.");
    }

    private static void ValidateEmail(string email)
    {
        try { _ = new MailAddress(email); }
        catch { throw new ArgumentException("Enter a valid email address."); }
        if (email.Length > 320) throw new ArgumentException("Email must contain up to 320 characters.");
    }

    private static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 15 or > 128)
            throw new ArgumentException("Administrator passwords must contain 15–128 characters.");
        var salt = RandomNumberGenerator.GetBytes(32);
        return (Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32), salt);
    }
}
