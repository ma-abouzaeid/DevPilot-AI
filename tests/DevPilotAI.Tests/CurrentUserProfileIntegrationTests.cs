// Integration tests for current user profile endpoints
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DevPilotAI.API.Models;
using DevPilotAI.Application.Services;
using DevPilotAI.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Infrastructure.Persistence;

namespace DevPilotAI.Tests.Integration;

public class CurrentUserProfileIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

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
                configureServices?.Invoke(services);
            });
        }).CreateClient();
        client.DefaultRequestHeaders.Add("Test-UserId", userId.ToString());
        return client;
    }

    [Fact]
    public async Task GetCurrentProfile_UserNotFound_Returns404()
    {
        var client = CreateClient(9999);
        var response = await client.GetAsync("/api/Users/me/Profile");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(payload!.success);
        Assert.Equal("NOT_FOUND", payload.errorCode);
    }

    [Fact]
    public async Task UpdateCurrentProfile_InvalidName_Returns400()
    {
        var client = CreateClient(42);
        var request = new UpdateUserProfileRequestDto("A"); // Too short
        var response = await client.PutAsJsonAsync("/api/Users/me/Update/Profile", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\": false", json);
    }

    [Fact]
    public async Task UpdateCurrentProfile_AuditFails_Returns500_And_NamePersists()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAuditService));
                if (descriptor != null) services.Remove(descriptor);
                services.AddTransient<IAuditService, FailingAuditService>();

                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApplicationDbContext));
                if (dbDescriptor != null) services.Remove(dbDescriptor);
            services.AddDbContext<IApplicationDbContext, DevPilotDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>() as DevPilotDbContext;
                ctx!.Database.EnsureCreated();
                ctx.Users.Add(new User { UserId = 55, Name = "Original", Email = "test@example.com" });
                ctx.SaveChanges();
            });
        });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Test-UserId", "55");
        var request = new UpdateUserProfileRequestDto("Updated Name");
        var response = await client.PutAsJsonAsync("/api/Users/me/Update/Profile", request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<IApplicationDbContext>() as DevPilotDbContext;
        var user = await db!.Users.FirstAsync(u => u.UserId == 55);
        Assert.Equal("Updated Name", user.Name);
    }
}

public class NoOpAuditService : IAuditService
{
    public Task LogAsync(string action, string entityName, string entityId, object? oldValues = null, object? newValues = null, int? userId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FailingAuditService : IAuditService
{
    public Task LogAsync(string action, string entityName, string entityId, object? oldValues = null, object? newValues = null, int? userId = null, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Audit logging failed");
    }

    public Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
