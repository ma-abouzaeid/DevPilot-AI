using System.Security.Claims;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.API.Authorization;

public sealed class AdminRoleRequirement : IAuthorizationRequirement
{
}

// Admin via JWT role or DB UserRoles (handles role granted after login).
public sealed class AdminRoleHandler : AuthorizationHandler<AdminRoleRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminRoleHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRoleRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (HasAdminClaim(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = GetUserId(context.User);
        if (userId is null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var isAdmin = await db.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId.Value && ur.Role.Name == AppRoles.Admin);

        if (isAdmin)
        {
            context.Succeed(requirement);
        }
    }

    private static bool HasAdminClaim(ClaimsPrincipal user)
    {
        if (user.IsInRole(AppRoles.Admin))
        {
            return true;
        }

        return user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role ||
             c.Type == "role" ||
             c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase) ||
             c.Type.EndsWith("/claims/role", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(c.Value, AppRoles.Admin, StringComparison.OrdinalIgnoreCase));
    }

    private static int? GetUserId(ClaimsPrincipal user)
    {
        var raw =
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue("sub") ??
            user.FindFirstValue("nameid");

        return int.TryParse(raw, out var userId) && userId > 0 ? userId : null;
    }
}
