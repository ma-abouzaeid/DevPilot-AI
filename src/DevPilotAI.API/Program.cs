using System.Security.Claims;
using System.Text;
using DevPilotAI.API.Middleware;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Services;
using DevPilotAI.Infrastructure.AIGateway;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var traceId = context.HttpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId)
                ? correlationId.ToString().Trim()
                : context.HttpContext.TraceIdentifier;

            var errors = context.ModelState
                .Where(ms => ms.Value?.Errors.Count > 0)
                .SelectMany(ms => ms.Value!.Errors.Select(e => new DevPilotAI.Application.Common.Models.ValidationErrorDetail
                {
                    Field = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(ms.Key.Replace("$.", "")),
                    Code = "VALIDATION_ERROR",
                    Message = e.ErrorMessage
                }))
                .ToList();

            var responsePayload = new DevPilotAI.Application.Common.Models.ApiResponse<object>
            {
                Success = false,
                Message = "Please correct the highlighted fields and try again.",
                Data = null,
                Errors = errors,
                ErrorCode = "VALIDATION_FAILED",
                TraceId = traceId
            };

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(responsePayload);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DevPilot AI API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string not found in configuration.");

builder.Services.AddDbContext<DevPilotDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOpts =>
    {
        sqlOpts.EnableRetryOnFailure();
    });
});

builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<DevPilotDbContext>());
builder.Services.AddHttpClient();

builder.Services.Configure<DevPilotAI.Infrastructure.Email.BrevoOptions>(builder.Configuration.GetSection(DevPilotAI.Infrastructure.Email.BrevoOptions.SectionName));
builder.Services.Configure<DevPilotAI.Infrastructure.Email.FrontendOptions>(builder.Configuration.GetSection(DevPilotAI.Infrastructure.Email.FrontendOptions.SectionName));

builder.Services.AddHttpClient<IEmailService, DevPilotAI.Infrastructure.Email.BrevoEmailService>((serviceProvider, client) =>
{
    var brevoConfig = builder.Configuration.GetSection(DevPilotAI.Infrastructure.Email.BrevoOptions.SectionName).Get<DevPilotAI.Infrastructure.Email.BrevoOptions>() ?? new DevPilotAI.Infrastructure.Email.BrevoOptions();
    var baseUri = string.IsNullOrWhiteSpace(brevoConfig.BaseUrl) ? "https://api.brevo.com" : brevoConfig.BaseUrl;
    client.BaseAddress = new Uri(baseUri);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITurnstileService, TurnstileService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuditService, DevPilotAI.Infrastructure.Audit.AuditService>();
builder.Services.AddScoped<IAIGateway, AIGatewayService>();
builder.Services.AddScoped<IProblemService, ProblemService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddDevPilotRateLimiting(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Contains("Change_In_Production", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Production configuration error: JwtSettings:Secret must be explicitly configured with a secure key and cannot use default placeholders.");
    }
}
else
{
    jwtSecret = string.IsNullOrWhiteSpace(jwtSecret) ? "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!" : jwtSecret;
}

var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "DevPilotAI";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "DevPilotAIUsers";

builder.Services.AddAuthentication(options =>
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
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

            var responsePayload = new DevPilotAI.Application.Common.Models.ApiResponse<object>
            {
                Success = false,
                Message = "Authentication is required to access this resource.",
                Data = null,
                Errors = new List<DevPilotAI.Application.Common.Models.ValidationErrorDetail>(),
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

            var responsePayload = new DevPilotAI.Application.Common.Models.ApiResponse<object>
            {
                Success = false,
                Message = "You do not have permission to access this resource.",
                Data = null,
                Errors = new List<DevPilotAI.Application.Common.Models.ValidationErrorDetail>(),
                ErrorCode = "FORBIDDEN",
                TraceId = traceId
            };

            await context.Response.WriteAsJsonAsync(responsePayload);
        }
    };
});

var rawAllowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
if (rawAllowedOrigins == null || rawAllowedOrigins.Length == 0)
{
    var commaOrigins = builder.Configuration["AllowedOrigins"];
    if (!string.IsNullOrWhiteSpace(commaOrigins))
    {
        rawAllowedOrigins = commaOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

var allowedOrigins = (rawAllowedOrigins != null && rawAllowedOrigins.Length > 0)
    ? rawAllowedOrigins.Select(o => o.Trim().TrimEnd('/')).Distinct().ToArray()
    : new[] { "https://devpilot-ai-three.vercel.app", "http://localhost:3000"};

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevPilotCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none';");
    await next();
});

var enableSwagger = builder.Configuration.GetValue<bool>("Swagger:Enabled", false)
                    || app.Environment.IsDevelopment()
                    || app.Environment.IsStaging();

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DevPilot AI API v1");
        c.RoutePrefix = "swagger";
    });
}

app.MapGet("/", () => Results.Ok(new
{
    status = "Online",
    service = "DevPilot AI API",
    version = "v1"
}));

app.UseCors("DevPilotCorsPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
