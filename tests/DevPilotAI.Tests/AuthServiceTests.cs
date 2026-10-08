using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Services;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class AuthServiceTests
{
    private const string JwtSecret = "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!";
    private const string JwtIssuer = "DevPilotAI";
    private const string JwtAudience = "DevPilotAIUsers";

    private DevPilotDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DevPilotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new DevPilotDbContext(options);
    }

    private Mock<ITurnstileService> GetMockTurnstileService(bool isValid = true)
    {
        var mock = new Mock<ITurnstileService>();
        mock.Setup(t => t.VerifyTokenAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(isValid);
        return mock;
    }

    private Mock<IEmailService> GetMockEmailService()
    {
        var mock = new Mock<IEmailService>();
        mock.Setup(m => m.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(m => m.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private IJwtTokenGenerator GetJwtTokenGenerator()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", JwtSecret },
            { "JwtSettings:Issuer", JwtIssuer },
            { "JwtSettings:Audience", JwtAudience },
            { "JwtSettings:ExpiryMinutes", "60" }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        return new JwtTokenGenerator(configuration);
    }

    private static void ValidateModel(object model)
    {
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        if (!isValid)
        {
            throw new ValidationException(validationResults.First().ErrorMessage);
        }
    }

    [Fact]
    public async Task RegisterAsync_ValidData_ShouldCreateUserAndVerificationRecordAndSendEmail()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Mahmoud",
            Email = "developer@example.com",
            Password = "Password123"
        };

        ValidateModel(dto);

        var response = await service.RegisterAsync(dto);

        Assert.True(response.Success);
        Assert.Equal("developer@example.com", response.Email);
        Assert.True(response.RequiresVerification);

        var user = await dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == "developer@example.com");

        Assert.NotNull(user);
        Assert.Null(user.EmailVerifiedAt);
        Assert.NotEqual("Password123", user.PasswordHash);
        Assert.DoesNotContain("SHA256", user.PasswordHash);
        Assert.NotEmpty(user.UserRoles);
        Assert.Contains(user.UserRoles, ur => ur.Role.Name == "Developer");

        var verification = await dbContext.EmailVerifications.FirstOrDefaultAsync(v => v.UserId == user.UserId);
        Assert.NotNull(verification);
        Assert.Equal(0, verification.Attempts);

        mockEmail.Verify(m => m.SendVerificationEmailAsync(
            "developer@example.com",
            "Mahmoud",
            It.Is<string>(otp => otp.Length == 6),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_FailedTurnstile_ShouldThrowInvalidOperationException()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(false);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Bot User",
            Email = "bot@example.com",
            Password = "Password123"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(dto));
        mockEmail.Verify(m => m.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ShouldThrowInvalidOperationException()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Mahmoud",
            Email = "duplicate@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(dto);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(dto));
    }

    [Fact]
    public async Task LoginAsync_UnverifiedUser_ShouldThrowUnauthorizedAccessException()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Unverified User",
            Email = "unverified@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(dto);

        var loginDto = new LoginRequestDto
        {
            Email = "unverified@example.com",
            Password = "Password123"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(loginDto));
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ShouldThrowUnauthorizedAccessException()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Valid User",
            Email = "validuser@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(dto);

        var user = await dbContext.Users.FirstAsync(u => u.Email == "validuser@example.com");
        user.EmailVerifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var loginDto = new LoginRequestDto
        {
            Email = "validuser@example.com",
            Password = "WrongPassword123"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(loginDto));
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ShouldReturnValidJwtWithDeveloperRoleClaim()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Verified Developer",
            Email = "dev@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(dto);

        var user = await dbContext.Users.FirstAsync(u => u.Email == "dev@example.com");
        user.EmailVerifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var loginDto = new LoginRequestDto
        {
            Email = "dev@example.com",
            Password = "Password123"
        };

        var result = await service.LoginAsync(loginDto);

        Assert.True(result.Success);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal("dev@example.com", result.User.Email);

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = JwtIssuer,
            ValidAudience = JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(result.AccessToken, validationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);

        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("Developer", roleClaims);

        var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value;
        Assert.Equal("dev@example.com", emailClaim);

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(user.UserId.ToString(), idClaim);
    }

    [Fact]
    public async Task VerifyEmailAsync_MaxAttemptsExceeded_ShouldRejectVerification()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Max Attempts User",
            Email = "maxattempts@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(dto);

        var user = await dbContext.Users.FirstAsync(u => u.Email == "maxattempts@example.com");
        var verification = await dbContext.EmailVerifications.FirstAsync(v => v.UserId == user.UserId);
        verification.Attempts = 5;
        await dbContext.SaveChangesAsync();

        var verifyDto = new VerifyEmailRequestDto
        {
            Email = "maxattempts@example.com",
            Code = "123456"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyEmailAsync(verifyDto));
    }

    [Fact]
    public async Task ResendVerificationAsync_ShouldResendVerificationEmail()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var user = new User
        {
            Name = "Resend User",
            Email = "resend@example.com",
            PasswordHash = "HashedPassword123",
            EmailVerifiedAt = null
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var oldVerification = new EmailVerification
        {
            UserId = user.UserId,
            CodeHash = "old_hash",
            Method = "OTP",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        dbContext.EmailVerifications.Add(oldVerification);
        await dbContext.SaveChangesAsync();

        var resendDto = new ResendVerificationRequestDto { Email = "resend@example.com" };
        var response = await service.ResendVerificationAsync(resendDto);

        Assert.True(response.Success);
        mockEmail.Verify(m => m.SendVerificationEmailAsync(
            "resend@example.com",
            "Resend User",
            It.Is<string>(otp => otp.Length == 6),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationAsync_CooldownActive_ShouldThrowInvalidOperationException()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var dto = new RegisterRequestDto
        {
            Name = "Cooldown User",
            Email = "cooldown@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(dto);

        var resendDto = new ResendVerificationRequestDto { Email = "cooldown@example.com" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResendVerificationAsync(resendDto));
    }

    [Fact]
    public async Task RefreshTokenRotation_And_ReuseProtection_ShouldWorkCorrectly()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var registerDto = new RegisterRequestDto
        {
            Name = "Rotation User",
            Email = "rotation@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(registerDto);
        var user = await dbContext.Users.FirstAsync(u => u.Email == "rotation@example.com");
        user.EmailVerifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var loginResult = await service.LoginAsync(new LoginRequestDto { Email = "rotation@example.com", Password = "Password123" });

        var initialRefreshToken = loginResult.RefreshToken;

        var refreshResult = await service.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = initialRefreshToken });

        Assert.True(refreshResult.Success);
        Assert.NotNull(refreshResult.AccessToken);
        Assert.NotEqual(initialRefreshToken, refreshResult.RefreshToken);

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = JwtIssuer,
            ValidAudience = JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(refreshResult.AccessToken, validationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);
        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("Developer", roleClaims);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = initialRefreshToken }));
    }

    [Fact]
    public async Task LogoutAsync_ShouldInvalidateRefreshToken()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var registerDto = new RegisterRequestDto
        {
            Name = "Logout User",
            Email = "logout@example.com",
            Password = "Password123"
        };

        await service.RegisterAsync(registerDto);
        var user = await dbContext.Users.FirstAsync(u => u.Email == "logout@example.com");
        user.EmailVerifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var loginResult = await service.LoginAsync(new LoginRequestDto { Email = "logout@example.com", Password = "Password123" });

        var logoutResult = await service.LogoutAsync(new LogoutRequestDto { RefreshToken = loginResult.RefreshToken });
        Assert.True(logoutResult.Success);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = loginResult.RefreshToken }));
    }

    [Fact]
    public async Task ForgotPasswordAsync_ExistingEmail_ShouldSendResetEmail()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var user = new User
        {
            Name = "Reset User",
            Email = "reset@example.com",
            PasswordHash = "HashedPassword123"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var response = await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "reset@example.com" });

        Assert.True(response.Success);
        Assert.Contains("If the email is registered", response.Message);

        mockEmail.Verify(m => m.SendPasswordResetEmailAsync(
            "reset@example.com",
            "Reset User",
            It.Is<string>(t => t.StartsWith("reset_")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_NonExistentEmail_ShouldReturnSameSafeResponseWithoutCallingEmail()
    {
        var dbContext = GetInMemoryDbContext();
        var mockTurnstile = GetMockTurnstileService(true);
        var mockEmail = GetMockEmailService();
        var jwtGenerator = GetJwtTokenGenerator();
        var service = new AuthService(dbContext, mockTurnstile.Object, jwtGenerator, mockEmail.Object);

        var response = await service.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "nonexistent@example.com" });

        Assert.True(response.Success);
        Assert.Contains("If the email is registered", response.Message);

        mockEmail.Verify(m => m.SendPasswordResetEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("short", "Password must be at least 8 characters long.")]
    [InlineData("alllowercase", "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    [InlineData("ALLUPPERCASE", "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    [InlineData("NoDigitsHere", "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public void RegisterRequestDto_PasswordValidation_ShouldEnforceRules(string password, string expectedErrorMessage)
    {
        var dto = new RegisterRequestDto
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = password
        };

        var ex = Assert.Throws<ValidationException>(() => ValidateModel(dto));
        Assert.Equal(expectedErrorMessage, ex.Message);
    }
}
