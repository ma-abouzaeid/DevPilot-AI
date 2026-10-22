using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace DevPilotAI.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var traceId = context.HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId)
                        ? correlationId.ToString().Trim()
                        : context.HttpContext.TraceIdentifier;

                    var errors = context.ModelState
                        .Where(ms => ms.Value?.Errors.Count > 0)
                        .SelectMany(ms => ms.Value!.Errors.Select(e => new ValidationErrorDetail
                        {
                            Field = JsonNamingPolicy.CamelCase.ConvertName(ms.Key.Replace("$.", "")),
                            Code = "VALIDATION_ERROR",
                            Message = e.ErrorMessage
                        }))
                        .ToList();

                    var responsePayload = new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Please correct the highlighted fields and try again.",
                        Data = null,
                        Errors = errors,
                        ErrorCode = "VALIDATION_FAILED",
                        TraceId = traceId
                    };

                    return new BadRequestObjectResult(responsePayload);
                };
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerDocumentation();
        services.AddJwtAuthentication(configuration, environment);
        services.AddCorsPolicy(configuration);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "DevPilot AI API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter your JWT token directly.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var jwtSecret = JwtSecurityValidator.GetValidatedSecret(configuration, environment.IsProduction());
        var jwtIssuer = JwtSecurityValidator.GetIssuer(configuration);
        var jwtAudience = JwtSecurityValidator.GetAudience(configuration);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = JwtSecurityValidator.GetSecurityKey(jwtSecret),
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var dbContext = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
                    var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out var userId))
                    {
                        var userExists = await dbContext.Users.AnyAsync(u => u.UserId == userId);
                        if (!userExists)
                        {
                            context.Fail("User account is inactive or deleted.");
                        }
                    }
                    else
                    {
                        context.Fail("Invalid user identification in token.");
                    }
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var traceId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId)
                        ? correlationId.ToString().Trim()
                        : context.HttpContext.TraceIdentifier;

                    var responsePayload = new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Authentication is required to access this resource.",
                        Data = null,
                        Errors = new List<ValidationErrorDetail>(),
                        ErrorCode = "UNAUTHENTICATED",
                        TraceId = traceId
                    };

                    await context.Response.WriteAsJsonAsync(responsePayload);
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    var traceId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId)
                        ? correlationId.ToString().Trim()
                        : context.HttpContext.TraceIdentifier;

                    var responsePayload = new ApiResponse<object>
                    {
                        Success = false,
                        Message = "You do not have permission to access this resource.",
                        Data = null,
                        Errors = new List<ValidationErrorDetail>(),
                        ErrorCode = "FORBIDDEN",
                        TraceId = traceId
                    };

                    await context.Response.WriteAsJsonAsync(responsePayload);
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var rawAllowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>();
        if (rawAllowedOrigins == null || rawAllowedOrigins.Length == 0)
        {
            var commaOrigins = configuration["AllowedOrigins"];
            if (!string.IsNullOrWhiteSpace(commaOrigins))
            {
                rawAllowedOrigins = commaOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        var allowedOrigins = (rawAllowedOrigins != null && rawAllowedOrigins.Length > 0)
            ? rawAllowedOrigins.Select(o => o.Trim().TrimEnd('/')).Distinct().ToArray()
            : new[] { "https://devpilot-ai-three.vercel.app", "http://localhost:3000" };

        services.AddCors(options =>
        {
            options.AddPolicy("DevPilotCorsPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
