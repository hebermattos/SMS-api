using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Sms.Application.Security;

namespace Sms.Infrastructure.Security;

public sealed class AesGcmSmsContentProtector : ISmsContentProtector
{
    private const byte Version = 1;
    private readonly byte[] _masterKey;

    public AesGcmSmsContentProtector(IConfiguration configuration)
    {
        var encodedKey = configuration["Encryption:MasterKey"];
        if (string.IsNullOrWhiteSpace(encodedKey))
            throw new InvalidOperationException("Encryption:MasterKey is not configured.");

        try { _masterKey = Convert.FromBase64String(encodedKey); }
        catch (FormatException ex) { throw new InvalidOperationException("Encryption:MasterKey must be Base64 encoded.", ex); }

        if (_masterKey.Length != 32)
            throw new InvalidOperationException("Encryption:MasterKey must decode to exactly 32 bytes for AES-256.");
    }

    public string Protect(Guid tenantId, Guid messageId, string field, string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(plaintext);
        var key = DeriveTenantKey(tenantId);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag, AssociatedData(tenantId, messageId, field));

            var payload = new byte[1 + nonce.Length + tag.Length + ciphertext.Length];
            payload[0] = Version;
            Buffer.BlockCopy(nonce, 0, payload, 1, nonce.Length);
            Buffer.BlockCopy(tag, 0, payload, 13, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, payload, 29, ciphertext.Length);
            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public string Unprotect(Guid tenantId, Guid messageId, string field, string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedValue);
        var payload = Convert.FromBase64String(protectedValue);
        if (payload.Length < 29 || payload[0] != Version)
            throw new CryptographicException("Invalid encrypted SMS content payload.");

        var key = DeriveTenantKey(tenantId);
        var plaintext = new byte[payload.Length - 29];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(payload.AsSpan(1, 12), payload.AsSpan(29), payload.AsSpan(13, 16), plaintext,
                AssociatedData(tenantId, messageId, field));
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public byte[] Fingerprint(Guid tenantId, string purpose, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentNullException.ThrowIfNull(value);
        var key = DeriveTenantKey(tenantId);
        try { return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{purpose}:{value}")); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private byte[] DeriveTenantKey(Guid tenantId) => HMACSHA256.HashData(_masterKey,
        Encoding.UTF8.GetBytes($"sms-content-v1:{tenantId:N}"));

    private static byte[] AssociatedData(Guid tenantId, Guid messageId, string field) =>
        Encoding.UTF8.GetBytes($"{tenantId:N}:{messageId:N}:{field}");
}
