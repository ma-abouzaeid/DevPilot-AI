using DevPilotAI.Application.Admin;
using DevPilotAI.Application.Authentication;
using DevPilotAI.Application.Challenges;
using DevPilotAI.Application.GitHub;
using DevPilotAI.Application.Plans;
using DevPilotAI.Application.Problems;
using DevPilotAI.Application.Support;
using DevPilotAI.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace DevPilotAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProblemService, ProblemService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGitHubService, GitHubService>();
        services.AddScoped<IChallengeService, ChallengeService>();
        services.AddScoped<IPlanUsageService, PlanUsageService>();
        services.AddScoped<ICreditCostService, CreditCostService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISupportTicketService, SupportTicketService>();

        return services;
    }
}
