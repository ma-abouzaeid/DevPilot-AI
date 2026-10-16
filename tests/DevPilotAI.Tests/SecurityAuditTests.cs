using System.Security.Claims;
using System.Text.Json;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Authentication;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Users;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Audit;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class SecurityAuditTests
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

    [Fact]
    public async Task AuditService_PersistsLog_WithHttpEnrichment()
    {
        using var context = GetInMemoryDbContext();

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        httpContext.Request.Headers.UserAgent = "Mozilla/5.0 TestBrowser";
        httpContext.TraceIdentifier = "trace_req_999";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var mockAccessor = new Mock<IHttpContextAccessor>();
        mockAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var auditService = new AuditService(context, mockAccessor.Object, NullLogger<AuditService>.Instance);

        await auditService.LogAsync(
            action: SecurityAuditEventTypes.AccountCreated,
            entityName: "User",
            entityId: "42",
            oldValues: null,
            newValues: new { Name = "Audit Test", Email = "audit@example.com" });

        var savedLog = await context.AuditLogs.FirstOrDefaultAsync(l => l.Action == SecurityAuditEventTypes.AccountCreated);

        Assert.NotNull(savedLog);
        Assert.Equal("User", savedLog.EntityName);
        Assert.Equal("42", savedLog.EntityId);
        Assert.Equal(42, savedLog.UserId);
        Assert.Equal("192.168.1.100", savedLog.IpAddress);
        Assert.Equal("Mozilla/5.0 TestBrowser", savedLog.UserAgent);
        Assert.Equal("trace_req_999", savedLog.RequestId);
    }

    [Fact]
    public async Task AuditService_RedactsSensitiveFields_InPayload()
    {
        using var context = GetInMemoryDbContext();
        var mockAccessor = new Mock<IHttpContextAccessor>();
        var auditService = new AuditService(context, mockAccessor.Object, NullLogger<AuditService>.Instance);

        var sensitivePayload = new
        {
            Username = "developer",
            Password = "secret",
            AccessToken = "jwt",
            RefreshToken = "refresh",
            ApiKey = "api-secret",
            Nested = new
            {
                Token = "nested-secret"
            },
            SafeData = "public_information"
        };

        await auditService.LogAsync(
            action: "TEST_SECURITY_REDACTION",
            entityName: "AuthTest",
            entityId: "1",
            newValues: sensitivePayload);

        var log = await context.AuditLogs.FirstOrDefaultAsync(l => l.Action == "TEST_SECURITY_REDACTION");
        Assert.NotNull(log);
        Assert.NotNull(log.NewValues);

        using var doc = JsonDocument.Parse(log.NewValues);
        var root = doc.RootElement;

        Assert.Equal("[REDACTED]", root.GetProperty("Password").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("AccessToken").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("RefreshToken").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("ApiKey").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("Nested").GetProperty("Token").GetString());
        Assert.Equal("public_information", root.GetProperty("SafeData").GetString());
        Assert.Equal("developer", root.GetProperty("Username").GetString());
    }

    [Fact]
    public async Task AuthService_RegisterAndLogin_LogsSecurityEvents()
    {
        using var context = GetInMemoryDbContext();
        var mockTurnstile = new Mock<ITurnstileService>();
        mockTurnstile.Setup(t => t.VerifyTokenAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockEmail = new Mock<IEmailService>();
        var mockAccessor = new Mock<IHttpContextAccessor>();
        var auditService = new AuditService(context, mockAccessor.Object, NullLogger<AuditService>.Instance);

        var authService = new AuthService(
            context,
            mockTurnstile.Object,
            GetJwtTokenGenerator(),
            mockEmail.Object,
            auditService);

        var registerDto = new RegisterRequestDto
        {
            Name = "Audit User",
            Email = "audit-user@example.com",
            Password = "SecurePassword123!",
            TurnstileToken = "token_dummy"
        };

        await authService.RegisterAsync(registerDto);

        var createdLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.Action == SecurityAuditEventTypes.AccountCreated);
        Assert.NotNull(createdLog);
        Assert.Equal("User", createdLog.EntityName);

        var badLoginDto = new LoginRequestDto
        {
            Email = "audit-user@example.com",
            Password = "WrongPassword123!"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.LoginAsync(badLoginDto));

        var failedLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.Action == SecurityAuditEventTypes.LoginFailed);
        Assert.NotNull(failedLog);

        var user = await context.Users.FirstAsync(u => u.Email == "audit-user@example.com");
        user.EmailVerifiedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var goodLoginDto = new LoginRequestDto
        {
            Email = "audit-user@example.com",
            Password = "SecurePassword123!"
        };

        var loginResult = await authService.LoginAsync(goodLoginDto);
        Assert.True(loginResult.Success);

        var successLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.Action == SecurityAuditEventTypes.LoginSuccess);
        Assert.NotNull(successLog);
        Assert.Equal(user.UserId, successLog.UserId);
    }

    [Fact]
    public async Task AuthService_RefreshTokenReuse_LogsReuseDetectionEvent()
    {
        using var context = GetInMemoryDbContext();
        var mockTurnstile = new Mock<ITurnstileService>();
        mockTurnstile.Setup(t => t.VerifyTokenAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var mockEmail = new Mock<IEmailService>();
        var mockAccessor = new Mock<IHttpContextAccessor>();
        var auditService = new AuditService(context, mockAccessor.Object, NullLogger<AuditService>.Instance);

        var authService = new AuthService(
            context,
            mockTurnstile.Object,
            GetJwtTokenGenerator(),
            mockEmail.Object,
            auditService);

        var user = new User
        {
            Name = "Token User",
            Email = "token-user@example.com",
            PasswordHash = "hashed",
            EmailVerifiedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var tokenRaw = "test_raw_token";
        var tokenHash = Convert.ToHexString(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(tokenRaw)));

        var tokenEntity = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            IsUsed = true,
            CreatedAt = DateTime.UtcNow
        };
        context.RefreshTokens.Add(tokenEntity);
        await context.SaveChangesAsync();

        var request = new RefreshTokenRequestDto
        {
            RefreshToken = tokenRaw
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.RefreshTokenAsync(request));

        var reuseLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.Action == SecurityAuditEventTypes.RefreshTokenReuse);

        Assert.NotNull(reuseLog);
        Assert.Equal(user.UserId, reuseLog.UserId);
    }

    [Fact]
    public async Task UserService_DeleteAccount_LogsSoftDelete_AndAuditLogPersists()
    {
        using var context = GetInMemoryDbContext();
        var mockAccessor = new Mock<IHttpContextAccessor>();
        var auditService = new AuditService(context, mockAccessor.Object, NullLogger<AuditService>.Instance);
        var userService = new UserService(context, auditService);

        var user = new User
        {
            Name = "To Delete",
            Email = "todelete@example.com",
            PasswordHash = "hash"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await userService.DeleteAccountAsync(user.UserId);

        var auditLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.Action == SecurityAuditEventTypes.AccountSoftDeleted && l.UserId == user.UserId);

        Assert.NotNull(auditLog);

        var userInDb = await context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == user.UserId);
        Assert.NotNull(userInDb);
        Assert.True(userInDb.IsDeleted);

        var totalLogs = await context.AuditLogs.CountAsync(l => l.UserId == user.UserId);
        Assert.Equal(1, totalLogs);
    }
}
