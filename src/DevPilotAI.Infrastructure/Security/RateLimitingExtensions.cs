using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevPilotAI.Infrastructure.Security;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddDevPilotRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var rateLimitOptions = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, cancellationToken) =>
            {
                await WriteRateLimitResponseAsync(context.HttpContext, context.Lease, cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.General, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.General.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.General.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.AiAnalysis, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.AiAnalysis.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.AiAnalysis.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.AiFollowUp, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.AiFollowUp.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.AiFollowUp.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.ArticleGeneration, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolvePartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.ArticleGeneration.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.ArticleGeneration.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.Login, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveClientIpKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.Login.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.Login.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.Register, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveClientIpKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.Register.PermitLimit,
                        Window = TimeSpan.FromSeconds(rateLimitOptions.Register.WindowSeconds),
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.AiConcurrency, httpContext =>
                RateLimitPartition.GetConcurrencyLimiter(
                    ResolvePartitionKey(httpContext),
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.AiConcurrency.PermitLimit,
                        QueueLimit = rateLimitOptions.AiConcurrency.QueueLimit
                    }));
        });

        return services;
    }

    private static string ResolvePartitionKey(HttpContext httpContext)
    {
        var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"usr_{userId}";
        }

        return ResolveClientIpKey(httpContext);
    }

    private static string ResolveClientIpKey(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(remoteIp))
        {
            return $"ip_{remoteIp}";
        }

        return "client_unknown";
    }

    public static async Task WriteRateLimitResponseAsync(HttpContext httpContext, RateLimitLease lease, CancellationToken cancellationToken = default)
    {
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "application/json";

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Max(1, retryAfter.TotalSeconds)).ToString();
        }
        else
        {
            httpContext.Response.Headers.RetryAfter = "60";
        }

        var requestId = httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : httpContext.TraceIdentifier;

        var responsePayload = new
        {
            success = false,
            errorCode = "RATE_LIMIT_EXCEEDED",
            message = "Too many requests. Please try again later.",
            requestId = requestId
        };

        await httpContext.Response.WriteAsJsonAsync(responsePayload, cancellationToken);
    }
}
