using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Services;

public class UserService : IUserService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuditService? _auditService;

    public UserService(IApplicationDbContext dbContext, IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task DeleteAccountAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User with ID {userId} was not found.");

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

    // Retrieve the current authenticated user's profile (safe fields only)
    public async Task<UserDto> GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User with ID {userId} was not found.");

        return new UserDto(user.UserId, user.Name, user.Email, user.EmailVerifiedAt);
    }

    // Update mutable profile fields (Name only) and return updated profile DTO.
    public async Task<UserDto> UpdateCurrentProfileAsync(int userId, UpdateUserProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw new KeyNotFoundException($"User with ID {userId} was not found.");

        // Update only allowed fields
        user.Name = request.Name;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            // Use generic audit action string as no specific enum exists for profile updates.
            await _auditService.LogAsync(
                action: "PROFILE_UPDATED",
                entityName: nameof(User),
                entityId: userId.ToString(),
                oldValues: null,
                newValues: new { Name = request.Name },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return new UserDto(user.UserId, user.Name, user.Email, user.EmailVerifiedAt);
    }



}
