using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DevPilotAI.Tests;

public class RateLimitingTests
{
    [Fact]
    public void RateLimitingOptions_Defaults_AreConfiguredCorrectly()
    {
        var options = new RateLimitingOptions();

        Assert.Equal(100, options.General.PermitLimit);
        Assert.Equal(60, options.General.WindowSeconds);

        Assert.Equal(5, options.AiAnalysis.PermitLimit);
        Assert.Equal(60, options.AiAnalysis.WindowSeconds);

        Assert.Equal(10, options.AiFollowUp.PermitLimit);
        Assert.Equal(60, options.AiFollowUp.WindowSeconds);

        Assert.Equal(3, options.ArticleGeneration.PermitLimit);
        Assert.Equal(60, options.ArticleGeneration.WindowSeconds);

        Assert.Equal(5, options.Login.PermitLimit);
        Assert.Equal(300, options.Login.WindowSeconds);

        Assert.Equal(5, options.Register.PermitLimit);
        Assert.Equal(300, options.Register.WindowSeconds);

        Assert.Equal(1, options.AiConcurrency.PermitLimit);
        Assert.Equal(0, options.AiConcurrency.QueueLimit);
    }

    [Fact]
    public void AddDevPilotRateLimiting_RegistersAllNamedPolicies()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddDevPilotRateLimiting(configuration);
        var serviceProvider = services.BuildServiceProvider();

        var rateLimiterOptions = serviceProvider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.NotNull(rateLimiterOptions);
        Assert.NotNull(rateLimiterOptions.OnRejected);
    }

    [Fact]
    public async Task ConcurrencyLimiter_ExceedingLimit_RejectsSubsequentRequest()
    {
        var options = new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0
        };

        using var limiter = new ConcurrencyLimiter(options);

        using var lease1 = limiter.AttemptAcquire(1);
        Assert.True(lease1.IsAcquired);

        using var lease2 = limiter.AttemptAcquire(1);
        Assert.False(lease2.IsAcquired);
    }

    [Fact]
    public async Task FixedWindowLimiter_ExceedingLimit_RejectsSubsequentRequest()
    {
        var options = new FixedWindowRateLimiterOptions
        {
            PermitLimit = 2,
            Window = TimeSpan.FromSeconds(60),
            QueueLimit = 0
        };

        using var limiter = new FixedWindowRateLimiter(options);

        using var lease1 = limiter.AttemptAcquire(1);
        Assert.True(lease1.IsAcquired);

        using var lease2 = limiter.AttemptAcquire(1);
        Assert.True(lease2.IsAcquired);

        using var lease3 = limiter.AttemptAcquire(1);
        Assert.False(lease3.IsAcquired);
    }

    [Fact]
    public async Task WriteRateLimitResponseAsync_Sets429Status_AndStandardResponseEnvelope()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.TraceIdentifier = "req_test_12345";

        var lease = new TestFailedRateLimitLease();
        await RateLimitingExtensions.WriteRateLimitResponseAsync(httpContext, lease);

        Assert.Equal(StatusCodes.Status429TooManyRequests, httpContext.Response.StatusCode);
        Assert.True(httpContext.Response.Headers.ContainsKey("Retry-After"));

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("RATE_LIMIT_EXCEEDED", root.GetProperty("errorCode").GetString());
        Assert.Equal("Too many requests. Please try again later.", root.GetProperty("message").GetString());
        Assert.Equal("req_test_12345", root.GetProperty("requestId").GetString());
    }

    private class TestFailedRateLimitLease : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => new[] { MetadataName.RetryAfter.Name };

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = TimeSpan.FromSeconds(45);
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
