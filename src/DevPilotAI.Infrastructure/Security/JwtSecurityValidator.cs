using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DevPilotAI.Infrastructure.Security;

/// <summary>
/// Centralized validator and key resolver for JWT configuration across token generation and authentication.
/// Enforces HS256 cryptographic key material constraints (>= 256 bits / 32 bytes),
/// detects insecure default placeholders without false-positives on ordinary words,
/// and consistently handles both Base64-encoded keys and UTF-8 passphrases.
/// </summary>
public static class JwtSecurityValidator
{
    public const string DefaultDevelopmentSecret = "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!";
    public const int MinimumKeySizeBytes = 32; // 256 bits required for HMAC-SHA256

    /// <summary>
    /// Explicit, unambiguous placeholder tokens that indicate a default/template configuration.
    /// Ordinary words like "secret" or "default" are deliberately NOT placed here to avoid
    /// falsely rejecting legitimate high-entropy passphrases or random keys.
    /// </summary>
    private static readonly string[] PlaceholderSubstrings =
    [
        "Change_In_Production",
        "your_jwt_secret",
        "your-jwt-secret",
        "your_secret_key",
        "your-secret-key",
        "my_secret_key",
        "my-secret-key",
        "placeholder"
    ];

    /// <summary>
    /// Exact forbidden values. Rejected only if the entire configured secret equals one of these words.
    /// </summary>
    private static readonly HashSet<string> ExactForbiddenValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "default",
        "secret",
        "password",
        "changeme",
        "1234567890",
        "admin",
        "test"
    };

    public static string GetValidatedSecret(IConfiguration configuration, bool isProduction)
    {
        var secret = configuration["JwtSettings:Secret"];
        return ValidateSecret(secret, isProduction);
    }

    public static string ValidateSecret(string? secret, bool isProduction)
    {
        if (isProduction)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException("Production configuration error: JwtSettings:Secret must be explicitly configured with a secure key and cannot use default placeholders.");
            }

            var trimmed = secret.Trim();

            // 1. Exact match on known project development default or trivial dictionary words
            if (string.Equals(trimmed, DefaultDevelopmentSecret, StringComparison.Ordinal) ||
                ExactForbiddenValues.Contains(trimmed))
            {
                throw new InvalidOperationException("Production configuration error: JwtSettings:Secret cannot use insecure placeholder values in Production.");
            }

            // 2. Substring match on explicit placeholder markers
            foreach (var placeholder in PlaceholderSubstrings)
            {
                if (trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Production configuration error: JwtSettings:Secret cannot use insecure placeholder values in Production.");
                }
            }

            // 3. Cryptographic key material validation (HS256 requires >= 256 bits / 32 bytes)
            // If the key is Base64 encoded, verify actual decoded key material size,
            // not merely the Base64 character count.
            if (TryDecodeBase64Key(trimmed, out var base64Bytes))
            {
                // Explicit Base64 syntax (ends with '=' padding, or contains '+' or '/')
                if (trimmed.EndsWith('=') || trimmed.Contains('+') || trimmed.Contains('/'))
                {
                    if (base64Bytes.Length < MinimumKeySizeBytes)
                    {
                        throw new InvalidOperationException($"Production configuration error: JwtSettings:Secret is a Base64-encoded key providing only {base64Bytes.Length} bytes ({base64Bytes.Length * 8} bits) of key material. HS256 requires at least {MinimumKeySizeBytes} bytes (256 bits).");
                    }
                }
                // Protect against treating a 32-character Base64 key (which decodes to only 24 bytes / 192 bits) as 32 bytes
                else if (trimmed.Length == 32 && base64Bytes.Length == 24)
                {
                    throw new InvalidOperationException($"Production configuration error: JwtSettings:Secret is a Base64 string of 32 characters representing only 24 bytes (192 bits). HS256 requires at least {MinimumKeySizeBytes} bytes (256 bits) of decoded key material.");
                }
            }

            var utf8ByteCount = Encoding.UTF8.GetByteCount(trimmed);
            if (utf8ByteCount < MinimumKeySizeBytes)
            {
                throw new InvalidOperationException($"Production configuration error: JwtSettings:Secret must be at least {MinimumKeySizeBytes} bytes (256 bits) for HMAC-SHA256 security. Actual length: {utf8ByteCount} bytes.");
            }

            return trimmed;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return DefaultDevelopmentSecret;
            }

            var trimmed = secret.Trim();
            var utf8ByteCount = Encoding.UTF8.GetByteCount(trimmed);
            if (utf8ByteCount < MinimumKeySizeBytes)
            {
                throw new InvalidOperationException($"Development configuration error: JwtSettings:Secret must be at least {MinimumKeySizeBytes} bytes (256 bits). Actual length: {utf8ByteCount} bytes.");
            }

            return trimmed;
        }
    }

    /// <summary>
    /// Converts the configured secret string into the cryptographic key bytes consistently.
    /// If the secret is Base64 encoded (contains padding '=' or Base64 symbols '+' or '/'),
    /// it decodes the raw bytes; otherwise, it encodes via UTF-8.
    /// </summary>
    public static byte[] GetKeyBytes(string secret)
    {
        var trimmed = secret.Trim();
        if (TryDecodeBase64Key(trimmed, out var base64Bytes) &&
            (trimmed.EndsWith('=') || trimmed.Contains('+') || trimmed.Contains('/')))
        {
            if (base64Bytes.Length >= MinimumKeySizeBytes)
            {
                return base64Bytes;
            }
        }

        return Encoding.UTF8.GetBytes(trimmed);
    }

    /// <summary>
    /// Returns the SymmetricSecurityKey used for both token issuance and token validation.
    /// </summary>
    public static SymmetricSecurityKey GetSecurityKey(string secret)
    {
        return new SymmetricSecurityKey(GetKeyBytes(secret));
    }

    public static string GetIssuer(IConfiguration configuration)
    {
        return configuration["JwtSettings:Issuer"] ?? "DevPilotAI";
    }

    public static string GetAudience(IConfiguration configuration)
    {
        return configuration["JwtSettings:Audience"] ?? "DevPilotAIUsers";
    }

    private static bool TryDecodeBase64Key(string secret, out byte[] decodedBytes)
    {
        decodedBytes = [];
        var trimmed = secret.Trim();
        if (trimmed.Length < 4 || trimmed.Length % 4 != 0)
        {
            return false;
        }

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c == '+' || c == '/' || c == '='))
            {
                return false;
            }
        }

        try
        {
            decodedBytes = Convert.FromBase64String(trimmed);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
