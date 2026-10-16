using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using DevPilotAI.API.Controllers;
using DevPilotAI.API.Middleware;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Services;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class StandardizedValidationAndErrorHandlingTests
{
    private DevPilotDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DevPilotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new DevPilotDbContext(options);
    }

    private static List<ValidationResult> ValidateDto(object dto)
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ApiResponse_SuccessResult_ShouldContainAllStandardFields()
    {
        var testData = new { Id = 101, Status = "Active" };
        var response = ApiResponse<object>.SuccessResult(testData, "Record created successfully.", "trace-12345");

        Assert.True(response.Success);
        Assert.Equal("Record created successfully.", response.Message);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Errors);
        Assert.Null(response.ErrorCode);
        Assert.Equal("trace-12345", response.TraceId);
        Assert.Equal("trace-12345", response.RequestId);
    }

    [Fact]
    public void ApiResponse_FailureResult_ShouldContainErrorsAndErrorCode()
    {
        var errors = new List<ValidationErrorDetail>
        {
            new("email", "Email is required.", "REQUIRED_FIELD"),
            new("password", "Password must be at least 8 characters.", "MIN_LENGTH")
        };

        var response = ApiResponse<object>.FailureResult("Validation failed.", "VALIDATION_FAILED", errors, "trace-999");

        Assert.False(response.Success);
        Assert.Equal("Validation failed.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal(2, response.Errors.Count);
        Assert.Equal("VALIDATION_FAILED", response.ErrorCode);
        Assert.Equal("trace-999", response.TraceId);
        Assert.Equal("email", response.Errors[0].Field);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    public void RegisterRequestDto_InvalidEmail_ShouldFailDataAnnotations(string invalidEmail)
    {
        var dto = new RegisterRequestDto
        {
            Name = "Alice Doe",
            Email = invalidEmail,
            Password = "Password123"
        };

        var errors = ValidateDto(dto);
        Assert.Contains(errors, e => e.MemberNames.Contains("Email"));
    }

    [Theory]
    [InlineData("short")] // < 8
    [InlineData("alllowercase1")] // missing uppercase
    [InlineData("ALLUPPERCASE1")] // missing lowercase
    [InlineData("NoDigitsHere")] // missing digit
    public void RegisterRequestDto_WeakPassword_ShouldFailComplexityRegex(string weakPassword)
    {
        var dto = new RegisterRequestDto
        {
            Name = "Alice Doe",
            Email = "alice@example.com",
            Password = weakPassword
        };

        var errors = ValidateDto(dto);
        Assert.Contains(errors, e => e.MemberNames.Contains("Password"));
    }

    [Fact]
    public void CreateProblemDto_EmptyTitleAndShortDescription_ShouldFailValidation()
    {
        var dto = new CreateProblemDto(
            UserId: 1,
            Title: "",
            Description: "Short" // < 10
        );

        var errors = ValidateDto(dto);
        Assert.Contains(errors, e => e.MemberNames.Contains("Title"));
        Assert.Contains(errors, e => e.MemberNames.Contains("Description"));
    }

    [Fact]
    public void AddProblemRequestDto_ContentTooShort_ShouldFailValidation()
    {
        var dto = new AddProblemRequestDto(Content: "hi"); // < 5

        var errors = ValidateDto(dto);
        Assert.Contains(errors, e => e.MemberNames.Contains("Content"));
    }

    [Fact]
    public void FeedbackRequestDto_ZeroAttemptId_ShouldFailRangeValidation()
    {
        var dto = new FeedbackRequestDto(AttemptId: 0, IsSolved: true);

        var errors = ValidateDto(dto);
        Assert.Contains(errors, e => e.MemberNames.Contains("AttemptId"));
    }

    [Fact]
    public async Task ProblemService_ConcurrentSubmission_ShouldGenerateUniqueProblemCodes()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAi = new Mock<IAIGateway>();
        mockAi.Setup(a => a.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(true, "Solution", "MockProvider", "Model", 10, 20, 50));

        var service = new ProblemService(dbContext, mockAi.Object);

        var tasks = Enumerable.Range(1, 5).Select(i =>
            service.SubmitProblemAsync(new CreateProblemDto(
                UserId: 1,
                Title: $"Concurrent Problem {i}",
                Description: "Valid problem description for testing concurrency."
            ))
        ).ToList();

        var results = await Task.WhenAll(tasks);
        var codes = results.Select(r => r.ProblemCode).ToList();

        Assert.Equal(5, codes.Distinct().Count());
        Assert.Contains("DEV-000001", codes);
        Assert.Contains("DEV-000002", codes);
        Assert.Contains("DEV-000003", codes);
        Assert.Contains("DEV-000004", codes);
        Assert.Contains("DEV-000005", codes);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_AppErrorConflict_ShouldReturn409WithStandardEnvelope()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw AppError.Conflict("An account with this email address already exists.", "EMAIL_ALREADY_EXISTS");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.TraceIdentifier = "trace-test-409";

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("EMAIL_ALREADY_EXISTS", root.GetProperty("errorCode").GetString());
        Assert.Equal("An account with this email address already exists.", root.GetProperty("message").GetString());
        Assert.Equal("trace-test-409", root.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_AppErrorUnauthorized_ShouldReturn401WithStandardEnvelope()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw AppError.Unauthorized("Invalid email or password.", "INVALID_CREDENTIALS");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("INVALID_CREDENTIALS", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_AppErrorNotFound_ShouldReturn404WithStandardEnvelope()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw AppError.NotFound("Problem with ID 999 not found.");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("RESOURCE_NOT_FOUND", root.GetProperty("errorCode").GetString());
        Assert.Equal("Problem with ID 999 not found.", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_UnexpectedException_ShouldReturn500WithSafeMessage()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw new DivideByZeroException("Sensitive internal division error with database pointer 0x7FFF");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("INTERNAL_SERVER_ERROR", root.GetProperty("errorCode").GetString());
        Assert.Equal("An unexpected error occurred. Please try again later.", root.GetProperty("message").GetString());
        Assert.DoesNotContain("0x7FFF", body);
        Assert.DoesNotContain("DivideByZeroException", body);
    }

    [Fact]
    public async Task ProblemsController_SubmitProblem_AuthenticatedUser_ShouldBindUserIdFromToken()
    {
        var mockService = new Mock<IProblemService>();
        mockService.Setup(s => s.SubmitProblemAsync(It.IsAny<CreateProblemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DebuggingResultDto(10, "DEV-000010", 1, "Root", "Exp", "Solution", "Groq"));

        var controller = new ProblemsController(mockService.Object);
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "88")
        }, "mock"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claims }
        };

        var incomingDto = new CreateProblemDto(UserId: 0, Title: "Valid Title", Description: "Valid Description longer than ten");

        var result = await controller.SubmitProblem(incomingDto, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, createdResult.StatusCode);

        var response = Assert.IsType<ApiResponse<DebuggingResultDto>>(createdResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Your problem has been submitted successfully.", response.Message);

        mockService.Verify(s => s.SubmitProblemAsync(It.Is<CreateProblemDto>(d => d.UserId == 88), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_AuthenticatedUser_UnauthorizedAccessException_ShouldReturn403Forbidden()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw new UnauthorizedAccessException("You do not have access to this resource.");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42")
        }, "TestAuth"));
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("FORBIDDEN", root.GetProperty("errorCode").GetString());
        Assert.Equal("You do not have access to this resource.", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_AnonymousUser_UnauthorizedAccessException_ShouldReturn401Unauthorized()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw new UnauthorizedAccessException("Authentication required.");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext(); // Anonymous user
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("UNAUTHORIZED", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_DbUpdateConcurrencyException_ShouldReturn409Conflict()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw new DbUpdateConcurrencyException("Concurrency conflict occurred.");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("CONCURRENCY_CONFLICT", root.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_GenericDbUpdateException_ShouldReturn500WithoutLeakingDbDetails()
    {
        var loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = (ctx) => throw new DbUpdateException("Fatal database connection drop to internal host sql-cluster-primary:1433");

        var middleware = new ExceptionHandlingMiddleware(next, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(httpContext);

        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("INTERNAL_SERVER_ERROR", root.GetProperty("errorCode").GetString());
        Assert.Equal("An unexpected database error occurred while persisting changes.", root.GetProperty("message").GetString());
        Assert.DoesNotContain("sql-cluster-primary", body);
        Assert.DoesNotContain("1433", body);
    }

    [Fact]
    public async Task TurnstileService_WhenEnabledAndSecretKeyMissing_FailsClosed()
    {
        var mockConfig = new Mock<IConfiguration>();
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("true");
        mockConfig.Setup(c => c.GetSection("Turnstile:Enabled")).Returns(mockSection.Object);
        mockConfig.Setup(c => c["Turnstile:SecretKey"]).Returns(string.Empty);

        var mockClientFactory = new Mock<IHttpClientFactory>();
        var mockLogger = new Mock<ILogger<TurnstileService>>();

        var service = new TurnstileService(mockClientFactory.Object, mockConfig.Object, mockLogger.Object);

        var result = await service.VerifyTokenAsync("any-token", "127.0.0.1", CancellationToken.None);

        Assert.False(result); // Must fail closed!
    }

    [Fact]
    public async Task ProblemsController_MutatingEndpoints_WithoutUserClaim_ThrowsUnauthorized()
    {
        var mockService = new Mock<IProblemService>();
        var controller = new ProblemsController(mockService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) } // No claims
        };

        var dto = new CreateProblemDto(0, "Title", "Valid description that is longer than ten");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.SubmitProblem(dto, CancellationToken.None));
    }

    [Fact]
    public async Task ProblemService_AddProblemRequest_CrossUserModification_ThrowsForbidden()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAi = new Mock<IAIGateway>();
        var service = new ProblemService(dbContext, mockAi.Object);

        // Problem owned by User 1
        var problem = new Problem
        {
            ProblemCode = "DEV-000001",
            UserId = 1,
            Title = "User 1 Problem",
            Summary = "Summary",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Problems.Add(problem);
        await dbContext.SaveChangesAsync();

        var dto = new AddProblemRequestDto("Followup request text");

        // User 2 attempts to modify User 1's problem
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AddProblemRequestAsync(problem.ProblemId, dto, userId: 2, CancellationToken.None));

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, ex.Data["StatusCode"]);
        Assert.Equal("FORBIDDEN", ex.Data["ErrorCode"]);
    }
}
