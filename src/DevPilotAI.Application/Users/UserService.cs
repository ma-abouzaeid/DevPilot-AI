using System.Security.Cryptography;
using System.Text;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Users;

public class UserService : IUserService
{
    public const string EmailChangeMethod = "EmailChange";

    private readonly IApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IAuditService? _auditService;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public UserService(
        IApplicationDbContext dbContext,
        IEmailService emailService,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _emailService = emailService;
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

    public async Task<UserDto> GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        var pending = await GetPendingEmailChangeAsync(userId, cancellationToken);
        return ToDto(user, pending?.PendingEmail);
    }

    public async Task<UserDto> UpdateCurrentProfileAsync(int userId, UpdateUserProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
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

        var pending = await GetPendingEmailChangeAsync(userId, cancellationToken);
        return ToDto(user, pending?.PendingEmail);
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

        // Keep other sessions from using the old password via refresh tokens.
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

    public async Task<EmailChangeResponseDto> RequestEmailChangeAsync(
        int userId,
        RequestEmailChangeDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
        {
            throw AppError.Unauthorized("Current password is incorrect.", "INVALID_PASSWORD");
        }

        var newEmail = request.NewEmail.Trim().ToLowerInvariant();
        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw AppError.InvalidRequest("New email must be different from your current email.", "EMAIL_UNCHANGED", nameof(request.NewEmail));
        }

        var taken = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.UserId != userId && u.Email.ToLower() == newEmail && !u.IsDeleted, cancellationToken);
        if (taken)
        {
            throw AppError.Conflict("This email is already associated with another account.", "EMAIL_ALREADY_IN_USE");
        }

        var recent = await _dbContext.EmailVerifications
            .Where(v =>
                v.UserId == userId &&
                v.Method == EmailChangeMethod &&
                v.VerifiedAt == null &&
                v.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
            .FirstOrDefaultAsync(cancellationToken);
        if (recent != null)
        {
            throw AppError.Conflict("Please wait before requesting another email change code.", "RESEND_COOLDOWN");
        }

        // Invalidate previous pending email-change codes.
        var pendingOld = await _dbContext.EmailVerifications
            .Where(v => v.UserId == userId && v.Method == EmailChangeMethod && v.VerifiedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var old in pendingOld)
        {
            old.VerifiedAt = DateTime.UtcNow; // mark consumed/cancelled
        }

        var rawOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var verification = new EmailVerification
        {
            UserId = userId,
            CodeHash = HashString(rawOtp),
            PendingEmail = newEmail,
            Method = EmailChangeMethod,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.EmailVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationEmailAsync(newEmail, user.Name, rawOtp, cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                action: "EMAIL_CHANGE_REQUESTED",
                entityName: nameof(User),
                entityId: userId.ToString(),
                oldValues: new { user.Email },
                newValues: new { PendingEmail = newEmail },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return new EmailChangeResponseDto(
            Success: true,
            Message: $"We sent a verification code to {newEmail}. Enter it below to finish changing your email.",
            PendingEmail: newEmail
        );
    }

    public async Task<UserDto> ConfirmEmailChangeAsync(
        int userId,
        ConfirmEmailChangeDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        var verification = await GetPendingEmailChangeAsync(userId, cancellationToken)
            ?? throw AppError.Conflict("No pending email change found. Request a new code.", "NO_PENDING_EMAIL_CHANGE");

        if (verification.ExpiresAt <= DateTime.UtcNow)
        {
            throw AppError.Conflict("Verification code has expired. Please request a new one.", "CODE_EXPIRED");
        }

        if (verification.Attempts >= 5)
        {
            throw AppError.Conflict("Maximum verification attempts reached. Please request a new code.", "MAX_ATTEMPTS_EXCEEDED");
        }

        var codeHash = HashString(request.Code.Trim());
        if (verification.CodeHash != codeHash)
        {
            verification.Attempts++;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw AppError.Conflict("Invalid or expired verification code.", "INVALID_CODE");
        }

        var newEmail = (verification.PendingEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(newEmail))
        {
            throw AppError.Conflict("Pending email change is invalid. Please request a new code.", "NO_PENDING_EMAIL_CHANGE");
        }

        var taken = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.UserId != userId && u.Email.ToLower() == newEmail && !u.IsDeleted, cancellationToken);
        if (taken)
        {
            throw AppError.Conflict("This email is already associated with another account.", "EMAIL_ALREADY_IN_USE");
        }

        var oldEmail = user.Email;
        user.Email = newEmail;
        // Same account state; mark verified because the new mailbox just proved ownership.
        user.EmailVerifiedAt = DateTime.UtcNow;
        verification.VerifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                action: "EMAIL_CHANGED",
                entityName: nameof(User),
                entityId: userId.ToString(),
                oldValues: new { Email = oldEmail },
                newValues: new { Email = newEmail },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return ToDto(user, null);
    }

    public async Task<EmailChangeResponseDto> ResendEmailChangeAsync(int userId, CancellationToken cancellationToken = default)
    {
        var pending = await GetPendingEmailChangeAsync(userId, cancellationToken)
            ?? throw AppError.Conflict("No pending email change found. Start a new email change request.", "NO_PENDING_EMAIL_CHANGE");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User with ID {userId} was not found.");

        if (pending.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
        {
            throw AppError.Conflict("Please wait before requesting another email change code.", "RESEND_COOLDOWN");
        }

        var newEmail = pending.PendingEmail!;
        var rawOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        pending.VerifiedAt = DateTime.UtcNow; // retire old code

        var verification = new EmailVerification
        {
            UserId = userId,
            CodeHash = HashString(rawOtp),
            PendingEmail = newEmail,
            Method = EmailChangeMethod,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.EmailVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationEmailAsync(newEmail, user.Name, rawOtp, cancellationToken);

        return new EmailChangeResponseDto(
            Success: true,
            Message: $"A new verification code was sent to {newEmail}.",
            PendingEmail: newEmail
        );
    }

    private async Task<EmailVerification?> GetPendingEmailChangeAsync(int userId, CancellationToken cancellationToken)
    {
        return await _dbContext.EmailVerifications
            .Where(v =>
                v.UserId == userId &&
                v.Method == EmailChangeMethod &&
                v.VerifiedAt == null &&
                v.ExpiresAt > DateTime.UtcNow &&
                v.PendingEmail != null)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static UserDto ToDto(User user, string? pendingEmail) =>
        new(user.UserId, user.Name, user.Email, user.EmailVerifiedAt, pendingEmail);

    private static string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
