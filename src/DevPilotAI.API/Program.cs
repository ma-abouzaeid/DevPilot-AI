
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Linq;
using DevPilotAI.API.Middleware;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Services;
using DevPilotAI.Infrastructure.AIGateway;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DevPilot AI API",
        Version = "v1"
    });

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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    // In-memory database for environments without a configured connection string.
    builder.Services.AddDbContext<DevPilotDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
else
{
    builder.Services.AddDbContext<DevPilotDbContext>(options =>
        options.UseSqlServer(connectionString, sqlOpts =>
        {
            sqlOpts.EnableRetryOnFailure();
        }));
}

builder.Services.AddScoped<IApplicationDbContext>(
    provider => provider.GetRequiredService<DevPilotDbContext>());

builder.Services.AddHttpClient();

builder.Services.Configure<DevPilotAI.Infrastructure.Email.BrevoOptions>(
    builder.Configuration.GetSection(
        DevPilotAI.Infrastructure.Email.BrevoOptions.SectionName));

builder.Services.Configure<DevPilotAI.Infrastructure.Email.FrontendOptions>(
    builder.Configuration.GetSection(
        DevPilotAI.Infrastructure.Email.FrontendOptions.SectionName));

builder.Services.AddHttpClient<IEmailService,
    DevPilotAI.Infrastructure.Email.BrevoEmailService>((serviceProvider, client) =>
    {
        var brevoConfig = builder.Configuration
            .GetSection(DevPilotAI.Infrastructure.Email.BrevoOptions.SectionName)
            .Get<DevPilotAI.Infrastructure.Email.BrevoOptions>()
            ?? new DevPilotAI.Infrastructure.Email.BrevoOptions();

        var baseUri = string.IsNullOrWhiteSpace(brevoConfig.BaseUrl)
            ? "https://api.brevo.com"
            : brevoConfig.BaseUrl;

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
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!";

var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "DevPilotAI";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "DevPilotAIUsers";

var authenticationBuilder = builder.Services.AddAuthentication(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        options.DefaultAuthenticateScheme = "TestScheme";
        options.DefaultChallengeScheme = "TestScheme";
    }
    else
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }
});

authenticationBuilder.AddJwtBearer(options =>
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
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var dbContext = context.HttpContext.RequestServices
                .GetRequiredService<IApplicationDbContext>();

            var userIdClaim = context.Principal?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(userIdClaim, out var userId))
            {
                var userExists = await dbContext.Users
                    .AnyAsync(u => u.UserId == userId);

                if (!userExists)
                {
                    context.Fail("User account is inactive or deleted.");
                }
            }
            else
            {
                context.Fail("Invalid user identification in token.");
            }
        }
    };
});

// Register the test authentication scheme only in the Testing environment.
if (builder.Environment.IsEnvironment("Testing"))
{
    authenticationBuilder.AddScheme<
        AuthenticationSchemeOptions,
        TestAuthHandler>("TestScheme", _ => { });
}

var rawAllowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>();

if (rawAllowedOrigins == null || rawAllowedOrigins.Length == 0)
{
    var commaOrigins = builder.Configuration["AllowedOrigins"];

    if (!string.IsNullOrWhiteSpace(commaOrigins))
    {
        rawAllowedOrigins = commaOrigins.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
    }
}

var allowedOrigins = rawAllowedOrigins != null && rawAllowedOrigins.Length > 0
    ? rawAllowedOrigins
        .Select(o => o.Trim().TrimEnd('/'))
        .Distinct()
        .ToArray()
    : new[]
    {
        "https://devpilot-ai-three.vercel.app",
        "http://localhost:3000"
    };

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
    context.Response.Headers.Append(
        "Referrer-Policy",
        "strict-origin-when-cross-origin");
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; frame-ancestors 'none';");

    await next();
});

var enableSwagger =
    builder.Configuration.GetValue<bool>("Swagger:Enabled", false) ||
    app.Environment.IsDevelopment() ||
    app.Environment.IsStaging();

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

// Test authentication handler used only by integration tests.
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
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
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public partial class Program { }
