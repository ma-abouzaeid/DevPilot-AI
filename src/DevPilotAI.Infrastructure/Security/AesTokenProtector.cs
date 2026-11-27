using System.Security.Cryptography;
using System.Text;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Infrastructure.Security;

// AES-GCM for tokens across restarts; v1 payloads use Data Protection.
public sealed class AesTokenProtector : ITokenProtector
{
    private const string Prefix = "v2.";
    private readonly byte[] _key;
    private readonly IDataProtector? _legacy;

    public AesTokenProtector(IConfiguration configuration, IDataProtectionProvider? dataProtection = null)
    {
        var material =
            configuration["Security:TokenProtectionKey"]
            ?? configuration["JwtSettings:Secret"]
            ?? configuration["JwtSettings:SecretKey"]
            ?? JwtSecurityValidator.DefaultDevelopmentSecret;

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(material.Trim()));
        _legacy = dataProtection?.CreateProtector("DevPilot.GitHub.Tokens.v1");
    }

    public string Protect(string plainText)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainText);

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var payload = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, payload, nonce.Length + tag.Length, cipher.Length);
        return Prefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedText)
    {
        ArgumentException.ThrowIfNullOrEmpty(protectedText);

        if (protectedText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var payload = Convert.FromBase64String(protectedText[Prefix.Length..]);
            if (payload.Length < 12 + 16)
            {
                throw new CryptographicException("Protected payload is too short.");
            }

            var nonce = payload.AsSpan(0, 12);
            var tag = payload.AsSpan(12, 16);
            var cipher = payload.AsSpan(28);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }

        if (_legacy == null)
        {
            throw new CryptographicException("Legacy Data Protection payload cannot be read.");
        }

        return _legacy.Unprotect(protectedText);
    }
}
