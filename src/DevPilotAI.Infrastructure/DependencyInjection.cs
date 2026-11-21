using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Infrastructure.AIGateway;
using DevPilotAI.Infrastructure.Audit;
using DevPilotAI.Infrastructure.Email;
using DevPilotAI.Infrastructure.GitHub;
using DevPilotAI.Infrastructure.Payments;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevPilotAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? throw new InvalidOperationException("Connection string not found in configuration.");

        services.AddDbContext<DevPilotDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOpts =>
            {
                sqlOpts.EnableRetryOnFailure();
            });
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<DevPilotDbContext>());
        services.AddHttpClient();

        services.Configure<BrevoOptions>(configuration.GetSection(BrevoOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<PaymobOptions>(configuration.GetSection(PaymobOptions.SectionName));
        services.AddHttpClient<IPaymobGateway, PaymobGateway>();

        services.AddHttpClient<IEmailService, BrevoEmailService>((serviceProvider, client) =>
        {
            var brevoConfig = configuration.GetSection(BrevoOptions.SectionName).Get<BrevoOptions>() ?? new BrevoOptions();
            var baseUri = string.IsNullOrWhiteSpace(brevoConfig.BaseUrl) ? "https://api.brevo.com" : brevoConfig.BaseUrl;
            client.BaseAddress = new Uri(baseUri);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpContextAccessor();
        services.AddDataProtection();
        // Stable AES key (Jwt:Key / Security:TokenProtectionKey) so GitHub tokens survive app recycles.
        services.AddSingleton<ITokenProtector, AesTokenProtector>();
        services.AddScoped<ITurnstileService, TurnstileService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAIGateway, AIGatewayService>();
        services.AddScoped<ISimilarityEngine, SimilarityEngine>();

        services.Configure<GitHubOptions>(configuration.GetSection(GitHubOptions.SectionName));
        services.AddHttpClient<IGitHubClient, GitHubClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DevPilot-AI");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });

        services.AddDevPilotRateLimiting(configuration);

        return services;
    }
}
