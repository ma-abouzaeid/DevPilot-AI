using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Users;

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
}
