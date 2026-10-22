using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DevPilotAI.API.Extensions;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace DevPilotAI.Tests;

public class AuthenticationFlowAndSwaggerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private const string TestSecret = "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!";
    private const string TestIssuer = "DevPilotAI";
    private const string TestAudience = "DevPilotAIUsers";

    static AuthenticationFlowAndSwaggerTests()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Server=(localdb)\\mssqllocaldb;Database=DevPilotTestDB;Trusted_Connection=True;");
    }

    public AuthenticationFlowAndSwaggerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AddSwaggerDocumentation_ConfiguresStandardHttpBearerScheme()
    {
        var services = new ServiceCollection();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerDocumentation();
        var serviceProvider = services.BuildServiceProvider();

        var swaggerGenOptions = serviceProvider.GetRequiredService<IOptions<SwaggerGenOptions>>().Value;
        Assert.NotNull(swaggerGenOptions);

        var securitySchemes = swaggerGenOptions.SwaggerGeneratorOptions.SecuritySchemes;
        Assert.True(securitySchemes.ContainsKey("Bearer"), "SecuritySchemes should contain 'Bearer'.");

        var bearerScheme = securitySchemes["Bearer"];
        Assert.Equal(SecuritySchemeType.Http, bearerScheme.Type);
        Assert.Equal("bearer", bearerScheme.Scheme, ignoreCase: true);
        Assert.Equal("JWT", bearerScheme.BearerFormat);
        Assert.Equal(ParameterLocation.Header, bearerScheme.In);

        var securityRequirements = swaggerGenOptions.SwaggerGeneratorOptions.SecurityRequirements;
        Assert.NotEmpty(securityRequirements);
        var hasBearerRequirement = securityRequirements.Any(req =>
            req.Keys.Any(k => k.Reference != null && k.Reference.Id == "Bearer"));
        Assert.True(hasBearerRequirement, "OpenApiSecurityRequirement must require 'Bearer'.");
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAuthorizationHeader_Returns401UnauthenticatedEnvelope()
    {
        var client = CreateRealAuthClient();

        var response = await client.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("UNAUTHENTICATED", body.ErrorCode);
        Assert.Contains("Authentication is required", body.Message);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithRawTokenWithoutBearerPrefix_FailsWith401()
    {
        var client = CreateRealAuthClient();
        var token = GenerateValidToken(userId: 1, email: "user1@devpilot.ai");

        // Send raw JWT directly in Authorization header without the "Bearer " scheme prefix
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me");
        request.Headers.TryAddWithoutValidation("Authorization", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("UNAUTHENTICATED", body.ErrorCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithMalformedToken_Returns401()
    {
        var client = CreateRealAuthClient();

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer this.is.not.a.valid.jwt");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("UNAUTHENTICATED", body.ErrorCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidBearerToken_Succeeds()
    {
        var inMemoryDbName = Guid.NewGuid().ToString();
        var client = CreateRealAuthClient(inMemoryDbName);

        var token = GenerateValidToken(userId: 55, email: "developer55@devpilot.ai");

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        // User 55 exists in DB so DeleteAsync succeeds (or returns 200/204)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void AuthController_SupportsBothRefreshAndRefreshTokenRoutes()
    {
        var controllerType = typeof(DevPilotAI.API.Controllers.AuthController);
        var method = controllerType.GetMethod(nameof(DevPilotAI.API.Controllers.AuthController.RefreshToken));

        Assert.NotNull(method);
        var httpPostAttributes = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPostAttribute>()
            .Select(a => a.Template)
            .ToList();

        Assert.Contains("refresh", httpPostAttributes);
        Assert.Contains("refresh-token", httpPostAttributes);
    }

    private HttpClient CreateRealAuthClient(string? dbName = null)
    {
        var inMemoryDbName = dbName ?? Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<DevPilotDbContext>()
            .UseInMemoryDatabase(inMemoryDbName)
            .Options;
        var inMemoryDbContext = new DevPilotDbContext(options);
        inMemoryDbContext.Users.Add(new User
        {
            UserId = 55,
            Name = "Developer 55",
            Email = "developer55@devpilot.ai",
            PasswordHash = "hash123",
            EmailVerifiedAt = DateTime.UtcNow
        });
        inMemoryDbContext.SaveChanges();

        var userMock = new Moq.Mock<DevPilotAI.Application.Users.IUserService>();
        userMock.Setup(s => s.DeleteAccountAsync(55, Moq.It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "JwtSettings:Secret", TestSecret },
                    { "JwtSettings:Issuer", TestIssuer },
                    { "JwtSettings:Audience", TestAudience },
                    { "JwtSettings:ExpiryMinutes", "60" }
                });
            });
            builder.ConfigureServices(services =>
            {
                var appDbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApplicationDbContext));
                if (appDbDescriptor != null) services.Remove(appDbDescriptor);

                var devPilotDbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DevPilotDbContext));
                if (devPilotDbDescriptor != null) services.Remove(devPilotDbDescriptor);

                services.AddScoped<IApplicationDbContext>(_ => inMemoryDbContext);
                services.AddScoped<DevPilotDbContext>(_ => inMemoryDbContext);

                var userServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DevPilotAI.Application.Users.IUserService));
                if (userServiceDescriptor != null) services.Remove(userServiceDescriptor);
                services.AddScoped(_ => userMock.Object);
            });
        }).CreateClient();
    }

    private static string GenerateValidToken(int userId, string email)
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", TestSecret },
            { "JwtSettings:Issuer", TestIssuer },
            { "JwtSettings:Audience", TestAudience },
            { "JwtSettings:ExpiryMinutes", "60" }
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var generator = new JwtTokenGenerator(config);
        return generator.GenerateAccessToken(userId, email, "Developer " + userId, new[] { "Developer" });
    }
}
