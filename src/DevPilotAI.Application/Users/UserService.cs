using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Users;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditService? _auditService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UserService(
        IApplicationDbContext dbContext,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    // Test ctor still passes IEmailService.
    public UserService(
        IApplicationDbContext dbContext,
        IEmailService emailService,
        IAuditService? auditService = null)
        : this(dbContext, auditService)
    {
        _ = emailService;
    }

    public async Task DeleteAccountAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        if (user.IsDeleted)
        {
            return;
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SecurityAuditEventTypes.AccountSoftDeleted,
                nameof(User),
                userId.ToString(),
                oldValues: null,
                newValues: new { UserId = userId, DeletedAt = user.DeletedAt },
                userId: userId,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<UserDto> GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        return ToDto(user);
    }

    public async Task<UserDto> UpdateCurrentProfileAsync(int userId, UpdateUserProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        var name = request.Name.Trim();
        if (name.Length < 2)
        {
            throw AppError.InvalidRequest("Name must be between 2 and 150 characters.", "INVALID_REQUEST", nameof(request.Name));
        }

        user.Name = name;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                action: "PROFILE_UPDATED",
                entityName: nameof(User),
                entityId: userId.ToString(),
                oldValues: null,
                newValues: new { Name = name },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return ToDto(user);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            throw AppError.InvalidRequest("New password and confirmation do not match.", "PASSWORD_MISMATCH", nameof(request.ConfirmNewPassword));
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            throw AppError.InvalidRequest("New password must be different from the current password.", "PASSWORD_UNCHANGED", nameof(request.NewPassword));
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
        {
            throw AppError.Unauthorized("Current password is incorrect.", "INVALID_PASSWORD");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);

        // Revoke refresh tokens on password change.
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                action: "PASSWORD_CHANGED",
                entityName: nameof(User),
                entityId: userId.ToString(),
                oldValues: null,
                newValues: new { UserId = userId },
                userId: userId,
                cancellationToken: cancellationToken);
        }
    }

    private static UserDto ToDto(User user)
    {
        var roles = user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        if (roles.Count == 0)
        {
            roles.Add(AppRoles.Developer);
        }

        return new UserDto(user.UserId, user.Name, user.Email, user.EmailVerifiedAt, Roles: roles);
    }
}
