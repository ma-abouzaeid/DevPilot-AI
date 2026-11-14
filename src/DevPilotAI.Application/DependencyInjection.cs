using DevPilotAI.Application.Authentication;
using DevPilotAI.Application.GitHub;
using DevPilotAI.Application.Problems;
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

        return services;
    }
}
