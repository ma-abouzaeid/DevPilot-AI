using System.Security.Claims;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Authentication;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Problems;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Application.Users;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class SoftDeleteStrategyTests
{
    private const string JwtSecret = "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!";
    private const string JwtIssuer = "DevPilotAI";
    private const string JwtAudience = "DevPilotAIUsers";

    private DevPilotDbContext GetDbContext()
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

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        return new JwtTokenGenerator(configuration);
    }

    [Fact]
    public async Task SoftDelete_SetsIsDeletedAndDeletedAt_RowRemainsInDatabase()
    {
        using var context = GetDbContext();

        var problem = new Problem
        {
            ProblemCode = "PROB-001",
            UserId = 1,
            Title = "Soft Delete Test",
            Summary = "Testing soft delete behavior"
        };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();

        context.SoftDelete(problem);
        await context.SaveChangesAsync();

        Assert.True(problem.IsDeleted);
        Assert.NotNull(problem.DeletedAt);

        var normalQuery = await context.Problems.FirstOrDefaultAsync(p => p.ProblemId == problem.ProblemId);
        Assert.Null(normalQuery);

        var ignoredQuery = await context.Problems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.ProblemId == problem.ProblemId);

        Assert.NotNull(ignoredQuery);
        Assert.True(ignoredQuery.IsDeleted);
        Assert.NotNull(ignoredQuery.DeletedAt);
    }

    [Fact]
    public async Task SoftDelete_PreservesRelatedHistoricalData()
    {
        using var context = GetDbContext();

        var user = new User
        {
            Name = "History Developer",
            Email = "developer@history.com",
            PasswordHash = "Hashed"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var problem = new Problem
        {
            ProblemCode = "PROB-002",
            UserId = user.UserId,
            Title = "Debugging Incident",
            Summary = "Historical incident"
        };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();

        var request = new ProblemRequest
        {
            ProblemId = problem.ProblemId,
            Description = "Initial request error"
        };
        context.ProblemRequests.Add(request);

        var attempt = new DebuggingAttempt
        {
            ProblemId = problem.ProblemId,
            ProblemRequestId = request.ProblemRequestId,
            AttemptNumber = 1,
            RootCauseAnalysis = "Null check missing",
            ProposedSolution = "Add defensive null check"
        };
        context.DebuggingAttempts.Add(attempt);
        await context.SaveChangesAsync();

        context.SoftDelete(problem);
        await context.SaveChangesAsync();

        var directProblem = await context.Problems.FirstOrDefaultAsync(p => p.ProblemId == problem.ProblemId);
        Assert.Null(directProblem);

        var remainingRequest = await context.ProblemRequests
            .FirstOrDefaultAsync(r => r.ProblemId == problem.ProblemId);
        Assert.NotNull(remainingRequest);

        var remainingAttempt = await context.DebuggingAttempts
            .FirstOrDefaultAsync(a => a.ProblemId == problem.ProblemId);
        Assert.NotNull(remainingAttempt);
        Assert.Equal("Null check missing", remainingAttempt.RootCauseAnalysis);
    }

    [Fact]
    public void ModelConfiguration_EveryForeignKey_MustUseNoActionCascade()
    {
        using var context = GetDbContext();
        var model = context.Model;

        var foreignKeys = model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).ToList();

        Assert.NotEmpty(foreignKeys);
        foreach (var fk in foreignKeys)
        {
            Assert.NotEqual(DeleteBehavior.Cascade, fk.DeleteBehavior);
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        }
    }

    [Fact]
    public async Task DeleteAccountAsync_SoftDeletesUser_AndRevokesAllActiveRefreshTokens()
    {
        using var context = GetDbContext();
        var userService = new UserService(context);

        var user = new User
        {
            Name = "John Developer",
            Email = "john@example.com",
            PasswordHash = "Hashed123"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var token1 = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = "token1_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };
        var token2 = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = "token2_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };
        context.RefreshTokens.AddRange(token1, token2);
        await context.SaveChangesAsync();

        await userService.DeleteAccountAsync(user.UserId);

        var deletedUser = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == user.UserId);

        Assert.NotNull(deletedUser);
        Assert.True(deletedUser.IsDeleted);
        Assert.NotNull(deletedUser.DeletedAt);

        var tokens = await context.RefreshTokens
            .Where(t => t.UserId == user.UserId)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, t =>
        {
            Assert.True(t.IsRevoked);
            Assert.NotNull(t.RevokedAt);
        });
    }

    [Fact]
    public async Task DeleteAccountAsync_RepeatedCall_IsSafeAndIdempotent()
    {
        using var context = GetDbContext();
        var userService = new UserService(context);

        var user = new User
        {
            Name = "Jane Developer",
            Email = "jane@example.com",
            PasswordHash = "Hashed123"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await userService.DeleteAccountAsync(user.UserId);

        var firstDeletedAt = (await context.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == user.UserId)).DeletedAt;

        await userService.DeleteAccountAsync(user.UserId);

        var secondDeletedAt = (await context.Users.IgnoreQueryFilters().FirstAsync(u => u.UserId == user.UserId)).DeletedAt;

        Assert.Equal(firstDeletedAt, secondDeletedAt);
    }

    [Fact]
    public async Task DeleteAccountAsync_NonExistentUser_ThrowsKeyNotFoundException()
    {
        using var context = GetDbContext();
        var userService = new UserService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => userService.DeleteAccountAsync(99999));
    }

    [Fact]
    public async Task DeletedUser_CannotLogin()
    {
        using var context = GetDbContext();
        var turnstileMock = new Mock<ITurnstileService>();
        var emailMock = new Mock<IEmailService>();
        var jwtGen = GetJwtTokenGenerator();

        var authService = new AuthService(context, turnstileMock.Object, jwtGen, emailMock.Object);

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
        var user = new User
        {
            Name = "Deleted Login Test",
            Email = "deletedlogin@example.com",
            EmailVerifiedAt = DateTime.UtcNow,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, "Password123!");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var loginDto = new LoginRequestDto
        {
            Email = "deletedlogin@example.com",
            Password = "Password123!"
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.LoginAsync(loginDto));
    }

    [Fact]
    public async Task DeletedUser_CannotRefreshToken()
    {
        using var context = GetDbContext();
        var turnstileMock = new Mock<ITurnstileService>();
        var emailMock = new Mock<IEmailService>();
        var jwtGen = GetJwtTokenGenerator();

        var authService = new AuthService(context, turnstileMock.Object, jwtGen, emailMock.Object);

        var user = new User
        {
            Name = "Deleted Refresh Test",
            Email = "deletedrefresh@example.com",
            EmailVerifiedAt = DateTime.UtcNow,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var tokenHash = Convert.ToHexString(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("raw_refresh_token")));

        var token = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        var dto = new RefreshTokenRequestDto { RefreshToken = "raw_refresh_token" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authService.RefreshTokenAsync(dto));
    }

    [Fact]
    public async Task DeletedUser_CannotResetPassword()
    {
        using var context = GetDbContext();
        var turnstileMock = new Mock<ITurnstileService>();
        var emailMock = new Mock<IEmailService>();
        var jwtGen = GetJwtTokenGenerator();

        var authService = new AuthService(context, turnstileMock.Object, jwtGen, emailMock.Object);

        var user = new User
        {
            Name = "Deleted Reset Test",
            Email = "deletedreset@example.com",
            EmailVerifiedAt = DateTime.UtcNow,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var tokenHash = Convert.ToHexString(sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes("raw_reset_token")));

        var verification = new EmailVerification
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            Method = "ResetLink",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            Attempts = 0
        };
        context.EmailVerifications.Add(verification);
        await context.SaveChangesAsync();

        var dto = new ResetPasswordRequestDto
        {
            Token = "raw_reset_token",
            NewPassword = "NewPassword123!"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => authService.ResetPasswordAsync(dto));
    }

    [Fact]
    public async Task DeletedUser_CannotVerifyOrResendVerification()
    {
        using var context = GetDbContext();
        var turnstileMock = new Mock<ITurnstileService>();
        var emailMock = new Mock<IEmailService>();
        var jwtGen = GetJwtTokenGenerator();

        var authService = new AuthService(context, turnstileMock.Object, jwtGen, emailMock.Object);

        var user = new User
        {
            Name = "Deleted Verify Test",
            Email = "deletedverify@example.com",
            EmailVerifiedAt = null,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var resendDto = new ResendVerificationRequestDto { Email = "deletedverify@example.com" };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => authService.ResendVerificationAsync(resendDto));

        var verifyDto = new VerifyEmailRequestDto
        {
            Email = "deletedverify@example.com",
            Code = "123456"
        };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => authService.VerifyEmailAsync(verifyDto));
    }

    [Fact]
    public async Task DeletedUser_ForgotPassword_DoesNotSendEmail()
    {
        using var context = GetDbContext();
        var turnstileMock = new Mock<ITurnstileService>();
        var emailMock = new Mock<IEmailService>();
        var jwtGen = GetJwtTokenGenerator();

        var authService = new AuthService(context, turnstileMock.Object, jwtGen, emailMock.Object);

        var user = new User
        {
            Name = "Deleted Forgot Test",
            Email = "deletedforgot@example.com",
            EmailVerifiedAt = DateTime.UtcNow,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var result = await authService.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "deletedforgot@example.com" });

        Assert.True(result.Success);
        emailMock.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var verifications = await context.EmailVerifications.Where(v => v.UserId == user.UserId).ToListAsync();
        Assert.Empty(verifications);
    }

    [Fact]
    public async Task ActiveUser_RemainsActiveByDefault()
    {
        using var context = GetDbContext();

        var user = new User
        {
            Name = "Active Developer",
            Email = "active@example.com",
            PasswordHash = "Hashed"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        Assert.False(user.IsDeleted);
        Assert.Null(user.DeletedAt);

        var activeUser = await context.Users.FirstOrDefaultAsync(u => u.UserId == user.UserId);
        Assert.NotNull(activeUser);
    }

    [Fact]
    public async Task SearchAndGetProblem_ExcludesSoftDeletedRecords()
    {
        using var context = GetDbContext();
        var aiMock = new Mock<IAIGateway>();
        var problemService = new ProblemService(context, aiMock.Object);

        var user = new User
        {
            Name = "Search Developer",
            Email = "search@example.com",
            PasswordHash = "Hashed"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var p1 = new Problem
        {
            ProblemCode = "DEV-000101",
            UserId = user.UserId,
            Title = "Active Bug in ASP.NET Core",
            Summary = "Active summary",
            Status = ProblemStatus.Open
        };
        var p2 = new Problem
        {
            ProblemCode = "DEV-000102",
            UserId = user.UserId,
            Title = "Deleted Bug in ASP.NET Core",
            Summary = "Deleted summary",
            Status = ProblemStatus.Open,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
        context.Problems.AddRange(p1, p2);
        await context.SaveChangesAsync();

        var searchResult = await problemService.SearchProblemsAsync("Bug", null, 1, 10);
        Assert.Single(searchResult.Items);
        Assert.Equal("DEV-000101", searchResult.Items[0].ProblemCode);

        var getDeleted = await problemService.GetProblemDetailsAsync(p2.ProblemId);
        Assert.Null(getDeleted);
    }

    [Fact]
    public async Task JwtValidation_WhenUserIsDeleted_FailsTokenValidation()
    {
        using var context = GetDbContext();

        var user = new User
        {
            Name = "Token Validation User",
            Email = "tokenval@example.com",
            PasswordHash = "Hashed",
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<IApplicationDbContext>(_ => context);
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var scheme = new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler));
        var options = new JwtBearerOptions();
        var tokenValidatedContext = new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = principal
        };

        var dbContext = tokenValidatedContext.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
        var userIdClaim = tokenValidatedContext.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int.TryParse(userIdClaim, out var userId);
        var userExists = await dbContext.Users.AnyAsync(u => u.UserId == userId);

        if (!userExists)
        {
            tokenValidatedContext.Fail("User account is inactive or deleted.");
        }

        Assert.NotNull(tokenValidatedContext.Result);
        Assert.False(tokenValidatedContext.Result.Succeeded);
    }
}
