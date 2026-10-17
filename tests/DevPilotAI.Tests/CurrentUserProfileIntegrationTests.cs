// Integration tests for current user profile endpoints
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Users;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevPilotAI.Tests.Integration;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Test-UserId", out var userIdValues) ||
            !int.TryParse(userIdValues.FirstOrDefault(), out var userId))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Missing or invalid Test-UserId header"));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"User_{userId}")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class CurrentUserProfileIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    static CurrentUserProfileIntegrationTests()
    {
        // Provide DefaultConnection in test process environment for WebApplicationFactory host building
        System.Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Server=(localdb)\\mssqllocaldb;Database=DevPilotTestDB;Trusted_Connection=True;");
    }

    public CurrentUserProfileIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(int userId, Action<IServiceCollection>? configureServices = null)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

                configureServices?.Invoke(services);
            });
        }).CreateClient();

        client.DefaultRequestHeaders.Add("Test-UserId", userId.ToString());
        return client;
    }

    [Fact]
    public async Task GetCurrentProfile_UserNotFound_Returns404()
    {
        var client = CreateClient(9999, services =>
        {
            var userMock = new Moq.Mock<IUserService>();
            userMock.Setup(s => s.GetCurrentProfileAsync(9999, Moq.It.IsAny<CancellationToken>()))
                .Throws(DevPilotAI.Application.Common.Exceptions.AppError.NotFound("User with ID 9999 was not found."));

            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserService));
            if (descriptor != null) services.Remove(descriptor);
            services.AddScoped(_ => userMock.Object);
        });

        var response = await client.GetAsync("/api/Users/me");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(payload!.Success);
        Assert.Equal("RESOURCE_NOT_FOUND", payload.ErrorCode);
    }

    [Fact]
    public async Task UpdateCurrentProfile_InvalidName_Returns400()
    {
        var client = CreateClient(42);
        var request = new UpdateUserProfileRequestDto("A"); // Too short
        var response = await client.PutAsJsonAsync("/api/Users/me/", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":false", json);
    }

    [Fact]
    public async Task UpdateCurrentProfile_AuditFails_Returns500_And_NamePersists()
    {
        User persistedUser = new User { UserId = 55, Name = "Original", Email = "test@example.com" };

        var userMock = new Moq.Mock<IUserService>();
        userMock.Setup(s => s.UpdateCurrentProfileAsync(55, Moq.It.IsAny<UpdateUserProfileRequestDto>(), Moq.It.IsAny<CancellationToken>()))
            .Callback<int, UpdateUserProfileRequestDto, CancellationToken>((id, req, ct) =>
            {
                persistedUser.Name = req.Name;
            })
            .Throws(new Exception("Audit logging failed"));

        var client = CreateClient(55, services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUserService));
            if (descriptor != null) services.Remove(descriptor);
            services.AddScoped(_ => userMock.Object);
        });

        var request = new UpdateUserProfileRequestDto("Updated Name");
        var response = await client.PutAsJsonAsync("/api/Users/me", request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Updated Name", persistedUser.Name);
    }
}
