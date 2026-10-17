# DevPilot AI — Clean Architecture Specification & System Design

**Project**: DevPilot AI  
**Runtime**: .NET 9 (C# 13) / ASP.NET Core Web API  
**Database**: SQL Server / Entity Framework Core 9  
**Reference Model**: Clean Architecture (adapted from enterprise Clean Architecture patterns)  
**Status**: Verified & Production Ready  

---

## 1. Architectural Overview & Dependency Inversion

DevPilot AI is architected according to the principles of Clean Architecture and Dependency Inversion. The core business rules and domain logic sit at the center of the system and remain completely decoupled from database frameworks, cloud providers, third-party APIs, and transport protocols.

### Project Dependency Graph

```text
                  +--------------------------------+
                  |         DevPilotAI.API         |
                  |  (Presentation & HTTP Gateway) |
                  +---------------+----------------+
                                  |
                   +--------------+-------------+
                   |                            | (DI composition only)
                   v                            v
    +------------------------------+  +------------------------------+
    |    DevPilotAI.Application    |  |   DevPilotAI.Infrastructure  |
    | (Use Cases, Abstractions, DI)|<-- (Technical Implementations,DI)|
    +--------------+---------------+  +--------------+---------------+
                   |                                 |
                   |       +-----------------+       |
                   +------>| DevPilotAI.Domain|<------+
                           |  (Core Entities,|
                           |  Enums, Rules)  |
                           +-----------------+
```

### Dependency Rules:
1. **DevPilotAI.Domain**: At the center. References **no other project** and has **zero external package dependencies**. Contains pure enterprise domain models, enums, and soft-delete contracts.
2. **DevPilotAI.Application**: References **only DevPilotAI.Domain**. It defines use cases, service contracts, data transfer objects, cross-cutting interfaces, domain exceptions, and standard response envelopes. It **never** references `Infrastructure` or `API`.
3. **DevPilotAI.Infrastructure**: References **DevPilotAI.Application** and **DevPilotAI.Domain**. It provides concrete technical implementations of application abstractions (EF Core DbContext, SQL Server migrations, Brevo email client, Cloudflare Turnstile verification, JWT token generation, security audit logging, sliding-window rate limiting, and AI provider routing).
4. **DevPilotAI.API**: The presentation layer and application composition root. References **DevPilotAI.Application** and **DevPilotAI.Infrastructure** exclusively to compose dependencies at startup (`Program.cs`), map HTTP endpoints, and coordinate middleware.

---

## 2. Project Responsibilities & Solution Layout

```text
DevPilotAI/
├── .config/
│   └── dotnet-tools.json                      # Solution-wide CLI tools (dotnet-ef 10.x)
├── DevPilotAI.slnx                            # Modern .NET 9 Solution definition
│
├── src/
│   ├── DevPilotAI.Domain/                     # Pure Enterprise Business Domain
│   │   ├── Common/
│   │   │   └── ISoftDeletable.cs              # Soft deletion contract
│   │   ├── Entities/                          # 24 Domain Entities
│   │   │   ├── AIModel.cs, AIProvider.cs, AIRequest.cs, AIUsageEvent.cs
│   │   │   ├── User.cs, Role.cs, UserRole.cs, UserSubscription.cs, RefreshToken.cs
│   │   │   ├── Problem.cs, ProblemRequest.cs, ProblemSolution.cs, DebuggingAttempt.cs
│   │   │   ├── Plan.cs, Feature.cs, PlanFeature.cs, UsagePeriod.cs, UsageLedger.cs
│   │   │   ├── KnowledgeArticle.cs, ArticleVersion.cs, Challenge.cs, CodeReview.cs
│   │   │   └── AuditLog.cs, EmailVerification.cs, GitHubAccount.cs, GitHubRepository.cs
│   │   ├── Enums/                             # Domain Enumerations
│   │   │   ├── AttemptResult.cs, ChallengeDifficulty.cs, ProblemStatus.cs
│   │   │   └── ProviderStatus.cs, ReviewSeverity.cs
│   │   └── DevPilotAI.Domain.csproj
│   │
│   ├── DevPilotAI.Application/                # Orchestration, Business Workflows & Interfaces
│   │   ├── Common/
│   │   │   ├── Exceptions/
│   │   │   │   └── AppExceptions.cs           # Standard domain exceptions (Validation, NotFound, etc.)
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs   # Persistence abstraction
│   │   │   │   ├── IAIGateway.cs              # AI routing & provider abstraction
│   │   │   │   ├── IEmailService.cs           # Transactional email delivery contract
│   │   │   │   ├── IJwtTokenGenerator.cs      # Token issuance contract
│   │   │   │   ├── ITurnstileService.cs       # Bot protection contract
│   │   │   │   └── IAuditService.cs           # Security audit logging contract
│   │   │   └── Models/
│   │   │       ├── ApiResponse.cs             # Universal ApiResponse<T> envelope & error models
│   │   │       └── PagedResultDto.cs          # Generic pagination model PagedResultDto<T>
│   │   ├── Authentication/                    # Authentication & Identity Feature
│   │   │   ├── DTOs/
│   │   │   │   ├── AuthRequestDtos.cs         # Register, Login, OTP, Reset Password requests
│   │   │   │   └── AuthResponseDtos.cs        # UserDto, Token responses, operation results
│   │   │   ├── AuthService.cs                 # Authentication business logic & orchestration
│   │   │   └── IAuthService.cs                # Authentication service contract
│   │   ├── Problems/                          # AI Debugging & Problem Resolution Feature
│   │   │   ├── DTOs/
│   │   │   │   ├── ProblemRequestDtos.cs      # CreateProblem, AddRequest, Feedback DTOs
│   │   │   │   └── ProblemResponseDtos.cs     # DebuggingResult, ProblemHistory, Search DTOs
│   │   │   ├── ProblemService.cs              # Problem submission, AI debugging orchestration
│   │   │   └── IProblemService.cs             # Problem service contract
│   │   ├── Users/                             # User Profile & Management Feature
│   │   │   ├── UserService.cs                 # Soft-delete & account management orchestration
│   │   │   └── IUserService.cs                # User service contract
│   │   ├── DependencyInjection.cs             # Application layer composition root
│   │   └── DevPilotAI.Application.csproj
│   │
│   ├── DevPilotAI.Infrastructure/             # Technical Implementations & External Adapters
│   │   ├── AIGateway/
│   │   │   └── AIGatewayService.cs            # Multi-provider routing, fallback & token accounting
│   │   ├── Audit/
│   │   │   └── AuditService.cs                # Database security audit event persistence
│   │   ├── Email/
│   │   │   ├── BrevoEmailService.cs           # Brevo (Sendinblue) HTTP API transactional client
│   │   │   ├── BrevoOptions.cs, FrontendOptions.cs
│   │   │   └── Templates/                     # HTML transactional email templates
│   │   ├── Migrations/                        # EF Core SQL Server Migrations
│   │   │   ├── 20261008160113_FullDB.cs
│   │   │   ├── 20261008194413_AddSoftDeleteEntities.cs
│   │   │   ├── 20261009131456_AddSecurityAuditLogs.cs
│   │   │   ├── 20261009140718_AddPlanAndUsageEntities.cs
│   │   │   └── DevPilotDbContextModelSnapshot.cs
│   │   ├── Persistence/
│   │   │   └── DevPilotDbContext.cs           # EF Core DbContext, global query filters, seeds
│   │   ├── Security/
│   │   │   ├── JwtTokenGenerator.cs           # HMAC-SHA256 JWT generation with claim mapping
│   │   │   ├── RateLimitingExtensions.cs      # Sliding-window & concurrency policy configurations
│   │   │   ├── RateLimitingOptions.cs         # Policy configuration models & policy constants
│   │   │   └── TurnstileService.cs            # Cloudflare Turnstile siteverify HTTP client
│   │   ├── DependencyInjection.cs             # Infrastructure layer composition root
│   │   └── DevPilotAI.Infrastructure.csproj
│   │
│   ├── DevPilotAI.API/                        # HTTP Presentation & Host
│   │   ├── Controllers/                       # Thin HTTP Controllers
│   │   │   ├── AuthController.cs              # Authentication & token endpoints
│   │   │   ├── ProblemsController.cs          # Debugging submission & feedback endpoints
│   │   │   └── UsersController.cs             # User profile & account management endpoints
│   │   ├── Extensions/                        # Modular API Configuration Extensions
│   │   │   ├── ServiceCollectionExtensions.cs # Swagger, JWT, CORS, ForwardedHeaders, ModelValidation
│   │   │   └── WebApplicationExtensions.cs    # Security headers middleware & Swagger UI pipeline
│   │   ├── Middleware/
│   │   │   └── ExceptionHandlingMiddleware.cs # Global exception handler, SQL mapping, status codes
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json, appsettings.Development.json, appsettings.Production.json
│   │   ├── Program.cs                         # Clean, declarative composition root (< 40 lines)
│   │   └── DevPilotAI.API.csproj
│   │
│   └── Frontend/                              # Responsive Web Client
│       ├── css/                               # Stylesheets & media queries
│       ├── js/                                # API client & UI modules
│       └── *.html                             # Auth & landing pages
│
└── tests/
    └── DevPilotAI.Tests/                      # Automated Verification Suite (97 tests)
        ├── AuthControllerTests.cs, AuthServiceTests.cs
        ├── BrevoEmailServiceTests.cs, DatabaseRelationshipTests.cs
        ├── LiveDatabaseCatalogTests.cs, PlanAndUsageEntityTests.cs
        ├── ProblemServiceTests.cs, RateLimitingTests.cs
        ├── SecurityAuditTests.cs, SoftDeleteStrategyTests.cs
        ├── StandardizedValidationAndErrorHandlingTests.cs
        └── UsersControllerTests.cs
```

---

## 3. Layer Composition Roots (Dependency Injection)

Rather than cluttering `Program.cs` with hundreds of lines of service registrations, each layer owns and maintains its own dependency registration method:

### 1. Application Layer (`DevPilotAI.Application/DependencyInjection.cs`)
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProblemService, ProblemService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
```

### 2. Infrastructure Layer (`DevPilotAI.Infrastructure/DependencyInjection.cs`)
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. SQL Server DbContext with transient error retries
        services.AddDbContext<DevPilotDbContext>(options => ...);
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<DevPilotDbContext>());

        // 2. External HTTP Integrations
        services.AddHttpClient();
        services.AddHttpClient<IEmailService, BrevoEmailService>(...);

        // 3. Security, Auditing & AI Services
        services.AddScoped<ITurnstileService, TurnstileService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAIGateway, AIGatewayService>();

        // 4. Rate Limiting Configuration
        services.AddDevPilotRateLimiting(configuration);

        return services;
    }
}
```

### 3. API Composition (`DevPilotAI.API/Program.cs`)
```csharp
var builder = WebApplication.CreateBuilder(args);

// Register layers via clean architecture composition roots
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging() || builder.Configuration.GetValue<bool>("Swagger:Enabled", false))
{
    app.UseSwaggerDocumentation();
}

app.UseCors("DevPilotCorsPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();
```

---

## 4. Main Request Flow Through the System

```text
  Client Request (HTTP/HTTPS)
             │
             ▼
  [ForwardedHeaders & Security Headers] (HSTS, CSP, X-Frame-Options)
             │
             ▼
  [ExceptionHandlingMiddleware] (Catches unhandled errors, maps SQL & domain codes)
             │
             ▼
  [DevPilot RateLimiter] (Enforces sliding/fixed-window policies per IP/user)
             │
             ▼
  [Authentication / JWT Bearer] (Validates signature, lifetime & soft-delete status)
             │
             ▼
  [API Controller] (Validates ModelState -> Returns standard ApiResponse<T>)
             │
             ▼
  [Application Service] (Orchestrates use cases, enforces business invariants)
             │
      ┌──────┴─────────────────────────────────┐
      │                                        │
      ▼                                        ▼
[IApplicationDbContext]                  [External Integrations]
(EF Core / SQL Server)                   (AIGateway, Brevo, Turnstile)
```

---

## 5. Architectural Decisions & Deviations from CompanyHub

| Aspect | CompanyHub Pattern | DevPilot AI Implementation | Architectural Rationale |
|--------|--------------------|----------------------------|-------------------------|
| **Project Count** | 4 projects (API, Application, Domain, Infrastructure) | **4 projects preserved** | Adding extra projects (e.g., SharedKernel, Contracts) would add maintenance overhead without technical benefit. |
| **Command/Query Dispatch** | MediatR / CQRS handlers for all actions | **Direct Service Abstractions** (`IAuthService`, `IProblemService`) | Avoided unnecessary MediatR indirection; existing use cases are cohesive, traceable, and fully testable without handler bloat. |
| **Data Mapping** | AutoMapper profiles | **Explicit DTO Records** & Pattern Matching | Direct type construction provides compile-time safety, zero reflection overhead, and transparent property tracing. |
| **Validation** | FluentValidation validators | **Data Annotations + Domain Exceptions** | ASP.NET Core native data annotations handle input validation cleanly; domain errors are raised via `AppError` exceptions caught by `ExceptionHandlingMiddleware`. |
| **EF Core Migrations** | Stored inside Infrastructure | **Maintained in `Infrastructure/Migrations/`** | Preserves existing migration history and EF Core tooling discovery without risking schema corruption. |
| **AI Gateway Architecture** | N/A (Standard CRUD) | **Dynamic Priority Routing & Failover** | DevPilot AI implements dynamic provider discovery (`AIProviders`), health tracking, token accounting, and fallback resilience. |
