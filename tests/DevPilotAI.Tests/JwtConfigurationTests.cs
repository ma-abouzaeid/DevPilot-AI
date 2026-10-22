using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DevPilotAI.API.Extensions;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class JwtConfigurationTests
{
    private const string ValidProductionSecret = "c7e8a9b0d1e2f3a4b5c6d7e8f90123456789abcdef0123456789abcdef01234567"; // 64-char hex string (64 bytes)
    private const string ValidDevelopmentSecret = "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!";

    [Fact]
    public void ValidProductionSecret_AllowsConfigurationValidationToSucceed()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", ValidProductionSecret },
            { "JwtSettings:Issuer", "DevPilotAI" },
            { "JwtSettings:Audience", "DevPilotAIUsers" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var result = JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true);

        Assert.Equal(ValidProductionSecret, result);
    }

    [Fact]
    public void RandomKey_ContainingOrdinaryWordSuchAsSecret_IsNotRejectedSolelyBecauseOfSubstring()
    {
        var validKeyWithSecretWord = "prod-secure-secret-key-material-with-sufficient-entropy-999!";
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", validKeyWithSecretWord }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var result = JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true);

        Assert.Equal(validKeyWithSecretWord, result);
    }

    [Fact]
    public void SecureRandomBase64Key_32Bytes_IsAccepted()
    {
        var random32Bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var base64Key = Convert.ToBase64String(random32Bytes);

        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", base64Key }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var result = JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true);

        Assert.Equal(base64Key, result);
    }

    [Fact]
    public void SecureRandomBase64Key_48Bytes_IsAccepted()
    {
        var random48Bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(48);
        var base64Key = Convert.ToBase64String(random48Bytes);

        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", base64Key }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var result = JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true);

        Assert.Equal(base64Key, result);
    }

    [Fact]
    public void UndersizedBase64Key_16BytesDecoded_IsRejected()
    {
        var random16Bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var base64Key = Convert.ToBase64String(random16Bytes); // 24 chars

        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", base64Key }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true));

        Assert.Contains("Production configuration error", ex.Message);
        Assert.DoesNotContain(base64Key, ex.Message);
    }

    [Fact]
    public void UndersizedBase64Key_24BytesDecoded_32Chars_IsRejected_DoesNotTreatCharacterCountAsCryptographicSize()
    {
        var random24Bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(24);
        var base64Key = Convert.ToBase64String(random24Bytes); // exactly 32 chars

        Assert.Equal(32, base64Key.Length);

        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", base64Key }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true));

        Assert.Contains("Production configuration error", ex.Message);
        Assert.DoesNotContain(base64Key, ex.Message);
    }

    [Fact]
    public void MissingProductionSecret_ThrowsInvalidOperationException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true));

        Assert.Contains("JwtSettings:Secret", ex.Message);
        Assert.Contains("Production configuration error", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void EmptyOrWhitespaceSecret_InProduction_ThrowsInvalidOperationException(string whitespaceSecret)
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", whitespaceSecret }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true));

        Assert.Contains("Production configuration error", ex.Message);
    }

    [Theory]
    [InlineData("DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!")]
    [InlineData("placeholder_super_secure_key_that_exceeds_32_bytes_length!")]
    [InlineData("your_jwt_secret_key_that_is_very_long_and_more_than_32_bytes!")]
    [InlineData("my_secret_key_for_jwt_tokens_that_is_definitely_32_bytes!")]
    public void KnownPlaceholderSecret_InProduction_ThrowsInvalidOperationException_WithoutLeakingSecret(string placeholderSecret)
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", placeholderSecret }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true));

        Assert.Contains("Production configuration error", ex.Message);
        Assert.DoesNotContain(placeholderSecret, ex.Message);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("secret")]
    [InlineData("password")]
    public void CommonForbiddenWords_InProduction_ThrowInvalidOperationException(string forbiddenWord)
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", forbiddenWord }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true));

        Assert.Contains("Production configuration error", ex.Message);
    }

    [Theory]
    [InlineData("short_16_byte_key")]
    [InlineData("exactly_31_bytes_long_secret_12")]
    [InlineData("undersized_key_not_32_bytes")]
    public void InadequatelySizedKey_ThrowsInvalidOperationException_WithoutLeakingSecret(string shortKey)
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", shortKey }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true));

        Assert.Contains("at least 32 bytes (256 bits)", ex.Message);
        Assert.DoesNotContain(shortKey, ex.Message);
    }

    [Fact]
    public void ValidDevelopmentConfiguration_FallsBackToDocumentedDefault_WhenSecretMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var result = JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: false);

        Assert.Equal(ValidDevelopmentSecret, result);
    }

    [Fact]
    public void ValidDevelopmentConfiguration_AcceptsCustomSecret_WhenProvided()
    {
        var customDevSecret = "custom_development_secret_key_exceeding_32_bytes_in_length!";
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", customDevSecret }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var result = JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: false);

        Assert.Equal(customDevSecret, result);
    }

    [Fact]
    public void JwtTokenGeneration_AndBearerTokenValidation_AreMutuallyCompatible()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", ValidProductionSecret },
            { "JwtSettings:Issuer", "DevPilotAI" },
            { "JwtSettings:Audience", "DevPilotAIUsers" },
            { "JwtSettings:ExpiryMinutes", "60" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var tokenGenerator = new JwtTokenGenerator(configuration, mockEnv.Object);
        var token = tokenGenerator.GenerateAccessToken(42, "developer@devpilot.ai", "Lead Developer", new[] { "Admin", "Developer" });

        Assert.NotNull(token);

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "DevPilotAI",
            ValidateAudience = true,
            ValidAudience = "DevPilotAIUsers",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ValidProductionSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

        Assert.NotNull(validatedToken);
        Assert.NotNull(principal);

        var nameIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var jwtSecurityToken = validatedToken as JwtSecurityToken;
        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
        var nameClaim = principal.FindFirst(ClaimTypes.Name)?.Value;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        Assert.Equal("42", nameIdClaim);
        Assert.Equal("42", jwtSecurityToken?.Subject);
        Assert.Equal("developer@devpilot.ai", emailClaim);
        Assert.Equal("Lead Developer", nameClaim);
        Assert.Contains("Admin", roles);
        Assert.Contains("Developer", roles);
    }

    [Fact]
    public void JwtTokenGeneration_AndBearerTokenValidation_CompatibleWithBase64Secret()
    {
        var random32Bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var base64Secret = Convert.ToBase64String(random32Bytes);

        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", base64Secret },
            { "JwtSettings:Issuer", "DevPilotAI" },
            { "JwtSettings:Audience", "DevPilotAIUsers" },
            { "JwtSettings:ExpiryMinutes", "60" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var mockEnv = new Mock<IHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var tokenGenerator = new JwtTokenGenerator(configuration, mockEnv.Object);
        var token = tokenGenerator.GenerateAccessToken(99, "user99@devpilot.ai", "Base64 User", new[] { "Developer" });

        Assert.NotNull(token);

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "DevPilotAI",
            ValidateAudience = true,
            ValidAudience = "DevPilotAIUsers",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtSecurityValidator.GetSecurityKey(base64Secret),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

        Assert.NotNull(validatedToken);
        Assert.NotNull(principal);
        Assert.Equal("99", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("user99@devpilot.ai", principal.FindFirst(ClaimTypes.Email)?.Value);
    }

    [Fact]
    public void EnvironmentVariableMapping_JwtSettingsSecret_CorrectlyPopulatesConfiguration()
    {
        const string envVarName = "JwtSettings__Secret";
        var expectedSecret = "env_var_configured_jwt_secret_with_more_than_32_characters_123456";

        var originalValue = Environment.GetEnvironmentVariable(envVarName);
        try
        {
            Environment.SetEnvironmentVariable(envVarName, expectedSecret);

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            Assert.Equal(expectedSecret, configuration["JwtSettings:Secret"]);

            var validatedSecret = JwtSecurityValidator.GetValidatedSecret(configuration, isProduction: true);
            Assert.Equal(expectedSecret, validatedSecret);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, originalValue);
        }
    }

    [Fact]
    public void AddJwtAuthentication_ProductionEnvironment_ConfiguresJwtBearer_WhenSecretValid()
    {
        var services = new ServiceCollection();
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", ValidProductionSecret },
            { "JwtSettings:Issuer", "DevPilotAI" },
            { "JwtSettings:Audience", "DevPilotAIUsers" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        services.AddJwtAuthentication(configuration, mockEnv.Object);
        var serviceProvider = services.BuildServiceProvider();

        var postConfigureOptions = serviceProvider.GetService<IPostConfigureOptions<JwtBearerOptions>>();
        Assert.NotNull(postConfigureOptions);
    }

    [Fact]
    public void AddJwtAuthentication_ProductionEnvironment_Throws_WhenSecretMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddJwtAuthentication(configuration, mockEnv.Object));

        Assert.Contains("Production configuration error", ex.Message);
    }
}
