using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DevPilotAI.Infrastructure.Security;

// JWT secret validation for HS256 (gen + auth).
public static class JwtSecurityValidator
{
    public const string DefaultDevelopmentSecret = "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!";
    public const int MinimumKeySizeBytes = 32; // HS256 wants at least 32 bytes

    // Obvious template strings in config (not bare words like "secret").
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

    // Whole-value blocklist for production.
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

            if (string.Equals(trimmed, DefaultDevelopmentSecret, StringComparison.Ordinal) ||
                ExactForbiddenValues.Contains(trimmed))
            {
                throw new InvalidOperationException("Production configuration error: JwtSettings:Secret cannot use insecure placeholder values in Production.");
            }

            foreach (var placeholder in PlaceholderSubstrings)
            {
                if (trimmed.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Production configuration error: JwtSettings:Secret cannot use insecure placeholder values in Production.");
                }
            }

            // Key length: decode Base64 first when it looks like Base64.
            if (TryDecodeBase64Key(trimmed, out var base64Bytes))
            {
                if (trimmed.EndsWith('=') || trimmed.Contains('+') || trimmed.Contains('/'))
                {
                    if (base64Bytes.Length < MinimumKeySizeBytes)
                    {
                        throw new InvalidOperationException($"Production configuration error: JwtSettings:Secret is a Base64-encoded key providing only {base64Bytes.Length} bytes ({base64Bytes.Length * 8} bits) of key material. HS256 requires at least {MinimumKeySizeBytes} bytes (256 bits).");
                    }
                }
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

    // Base64 secret -> raw bytes; otherwise UTF-8.
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