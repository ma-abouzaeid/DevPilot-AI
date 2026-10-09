using System.Security.Cryptography;
using System.Text;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITurnstileService _turnstileService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;
    private readonly IAuditService? _auditService;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthService(
        IApplicationDbContext dbContext,
        ITurnstileService turnstileService,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _turnstileService = turnstileService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailService = emailService;
        _auditService = auditService;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        var isValidBotCheck = await _turnstileService.VerifyTokenAsync(dto.TurnstileToken, null, cancellationToken);
        if (!isValidBotCheck)
        {
            throw new InvalidOperationException("Security check failed. Please complete the captcha verification.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        var tempUser = new User { Email = normalizedEmail };
        var passwordHash = _passwordHasher.HashPassword(tempUser, dto.Password);

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            EmailVerifiedAt = null,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("An account with this email address already exists.");
        }

        var developerRole = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == "Developer", cancellationToken);
        if (developerRole == null)
        {
            developerRole = new Role { Name = "Developer" };
            _dbContext.Roles.Add(developerRole);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _dbContext.UserRoles.Add(new UserRole
        {
            UserId = user.UserId,
            RoleId = developerRole.RoleId
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        var defaultPlan = await _dbContext.Plans
            .FirstOrDefaultAsync(p => p.IsDefault && p.Status == "Active", cancellationToken);
        if (defaultPlan != null)
        {
            _dbContext.UserSubscriptions.Add(new UserSubscription
            {
                UserId = user.UserId,
                PlanId = defaultPlan.PlanId,
                Status = "Active",
                StartDate = DateTime.UtcNow,
                AssignmentSource = "Registration",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var rawOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var otpHash = HashString(rawOtp);

        var verification = new EmailVerification
        {
            UserId = user.UserId,
            CodeHash = otpHash,
            Method = "OTP",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.EmailVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationEmailAsync(user.Email, user.Name, rawOtp, cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SecurityAuditEventTypes.AccountCreated,
                nameof(User),
                user.UserId.ToString(),
                oldValues: null,
                newValues: new { user.Email, user.Name },
                userId: user.UserId,
                cancellationToken: cancellationToken);
        }

        return new RegisterResponseDto(
            Success: true,
            Message: "Registration successful. A verification code has been sent to your email.",
            Email: user.Email,
            RequiresVerification: true
        );
    }

    public async Task<VerifyEmailResponseDto> VerifyEmailAsync(VerifyEmailRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) && string.IsNullOrWhiteSpace(dto.Token))
        {
            throw new ArgumentException("Either verification code or verification token must be provided.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                throw new ArgumentException("Email is required when verifying with a code.");
            }

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken)
                ?? throw new KeyNotFoundException("User account not found.");

            if (user.IsDeleted)
            {
                throw new KeyNotFoundException("User account not found.");
            }

            if (user.EmailVerifiedAt.HasValue)
            {
                throw new InvalidOperationException("Email address is already verified.");
            }

            var codeHash = HashString(dto.Code.Trim());

            var verification = await _dbContext.EmailVerifications
                .Where(v => v.UserId == user.UserId && v.Method == "OTP" && v.VerifiedAt == null && v.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(v => v.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (verification == null || verification.Attempts >= 5)
            {
                throw new InvalidOperationException("Invalid or expired verification code.");
            }

            if (verification.CodeHash != codeHash)
            {
                verification.Attempts++;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("Invalid or expired verification code.");
            }

            verification.VerifiedAt = DateTime.UtcNow;
            user.EmailVerifiedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_auditService != null)
            {
                await _auditService.LogAsync(
                    SecurityAuditEventTypes.EmailVerified,
                    nameof(User),
                    user.UserId.ToString(),
                    oldValues: null,
                    newValues: new { user.Email, Method = "OTP" },
                    userId: user.UserId,
                    cancellationToken: cancellationToken);
            }

            return new VerifyEmailResponseDto(
                Success: true,
                Message: "Email verified successfully."
            );
        }

        var tokenToVerify = dto.Token!.Trim();
        if (tokenToVerify.Contains("token="))
        {
            var idx = tokenToVerify.IndexOf("token=");
            tokenToVerify = tokenToVerify.Substring(idx + 6);
            var ampIdx = tokenToVerify.IndexOf('&');
            if (ampIdx >= 0)
            {
                tokenToVerify = tokenToVerify.Substring(0, ampIdx);
            }
        }
        tokenToVerify = Uri.UnescapeDataString(tokenToVerify);

        var tokenHash = HashString(tokenToVerify);
        var tokenVerification = await _dbContext.EmailVerifications
            .Include(v => v.User)
            .Where(v => v.TokenHash == tokenHash && v.VerifiedAt == null && v.ExpiresAt > DateTime.UtcNow && v.Attempts < 5)
            .FirstOrDefaultAsync(cancellationToken);

        if (tokenVerification == null || tokenVerification.User == null || tokenVerification.User.IsDeleted)
        {
            throw new InvalidOperationException("Invalid or expired verification token.");
        }

        tokenVerification.VerifiedAt = DateTime.UtcNow;
        tokenVerification.User.EmailVerifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SecurityAuditEventTypes.EmailVerified,
                nameof(User),
                tokenVerification.User.UserId.ToString(),
                oldValues: null,
                newValues: new { tokenVerification.User.Email, Method = "Token" },
                userId: tokenVerification.User.UserId,
                cancellationToken: cancellationToken);
        }

        return new VerifyEmailResponseDto(
            Success: true,
            Message: "Email verified successfully."
        );
    }

    public async Task<ResendVerificationResponseDto> ResendVerificationAsync(ResendVerificationRequestDto dto, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken)
            ?? throw new KeyNotFoundException("User account not found.");

        if (user.IsDeleted)
        {
            throw new KeyNotFoundException("User account not found.");
        }

        if (user.EmailVerifiedAt.HasValue)
        {
            throw new InvalidOperationException("This account is already verified.");
        }

        var recentVerification = await _dbContext.EmailVerifications
            .Where(v => v.UserId == user.UserId && v.VerifiedAt == null && v.CreatedAt > DateTime.UtcNow.AddSeconds(-60))
            .FirstOrDefaultAsync(cancellationToken);

        if (recentVerification != null)
        {
            throw new InvalidOperationException("Please wait before requesting another verification code.");
        }

        var rawOtp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var otpHash = HashString(rawOtp);

        var verification = new EmailVerification
        {
            UserId = user.UserId,
            CodeHash = otpHash,
            Method = "OTP",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            Attempts = 0,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.EmailVerifications.Add(verification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationEmailAsync(user.Email, user.Name, rawOtp, cancellationToken);

        return new ResendVerificationResponseDto(
            Success: true,
            Message: "Verification code has been resent to your email."
        );
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user == null || user.IsDeleted)
        {
            if (_auditService != null)
            {
                await _auditService.LogAsync(
                    SecurityAuditEventTypes.LoginFailed,
                    nameof(User),
                    normalizedEmail,
                    oldValues: null,
                    newValues: new { Email = normalizedEmail, Reason = "User not found or deleted" },
                    userId: null,
                    cancellationToken: cancellationToken);
            }
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            if (_auditService != null)
            {
                await _auditService.LogAsync(
                    SecurityAuditEventTypes.LoginFailed,
                    nameof(User),
                    user.UserId.ToString(),
                    oldValues: null,
                    newValues: new { Email = normalizedEmail, Reason = "Password mismatch" },
                    userId: user.UserId,
                    cancellationToken: cancellationToken);
            }
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.EmailVerifiedAt.HasValue)
        {
            if (_auditService != null)
            {
                await _auditService.LogAsync(
                    SecurityAuditEventTypes.LoginFailed,
                    nameof(User),
                    user.UserId.ToString(),
                    oldValues: null,
                    newValues: new { Email = normalizedEmail, Reason = "Email not verified" },
                    userId: user.UserId,
                    cancellationToken: cancellationToken);
            }
            throw new UnauthorizedAccessException("Email is not verified. Please verify your account before logging in.");
        }

        var roles = user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => r!)
            .ToList();

        if (roles.Count == 0)
        {
            roles.Add("Developer");
        }

        var rawAccessToken = _jwtTokenGenerator.GenerateAccessToken(user.UserId, user.Email, user.Name, roles);
        var rawRefreshToken = GenerateSecureToken("refresh", user.UserId);
        var refreshTokenHash = HashString(rawRefreshToken);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SecurityAuditEventTypes.LoginSuccess,
                nameof(User),
                user.UserId.ToString(),
                oldValues: null,
                newValues: new { user.Email, user.Name, Roles = roles },
                userId: user.UserId,
                cancellationToken: cancellationToken);
        }

        var userDto = new UserDto(
            UserId: user.UserId,
            Name: user.Name,
            Email: user.Email,
            EmailVerifiedAt: user.EmailVerifiedAt
        );

        return new LoginResponseDto(
            Success: true,
            AccessToken: rawAccessToken,
            RefreshToken: rawRefreshToken,
            User: userDto
        );
    }

    public async Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(dto.RefreshToken));
        }

        var tokenHash = HashString(dto.RefreshToken.Trim());

        var tokenEntity = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity == null || tokenEntity.IsRevoked || tokenEntity.ExpiresAt <= DateTime.UtcNow || tokenEntity.User == null || tokenEntity.User.IsDeleted)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        if (tokenEntity.IsUsed)
        {
            var userTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == tokenEntity.UserId && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var t in userTokens)
            {
                t.IsRevoked = true;
                t.RevokedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_auditService != null)
            {
                await _auditService.LogAsync(
                    SecurityAuditEventTypes.RefreshTokenReuse,
                    nameof(RefreshToken),
                    tokenEntity.RefreshTokenId.ToString(),
                    oldValues: null,
                    newValues: new { UserId = tokenEntity.UserId, RevokedCount = userTokens.Count },
                    userId: tokenEntity.UserId,
                    cancellationToken: cancellationToken);
            }

            throw new UnauthorizedAccessException("Refresh token reuse detected. All active sessions have been revoked for security.");
        }

        var roles = tokenEntity.User.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(r => !string.IsNullOrEmpty(r))
            .Select(r => r!)
            .ToList();

        if (roles.Count == 0)
        {
            roles.Add("Developer");
        }

        var newRawAccessToken = _jwtTokenGenerator.GenerateAccessToken(
            tokenEntity.User.UserId,
            tokenEntity.User.Email,
            tokenEntity.User.Name,
            roles);

        var newRawRefreshToken = GenerateSecureToken("refresh", tokenEntity.UserId);
        var newRefreshTokenHash = HashString(newRawRefreshToken);

        tokenEntity.IsUsed = true;
        tokenEntity.ReplacedByTokenHash = newRefreshTokenHash;

        var newTokenEntity = new RefreshToken
        {
            UserId = tokenEntity.UserId,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(newTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResponseDto(
            Success: true,
            AccessToken: newRawAccessToken,
            RefreshToken: newRawRefreshToken
        );
    }

    public async Task<LogoutResponseDto> LogoutAsync(LogoutRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(dto.RefreshToken));
        }

        var tokenHash = HashString(dto.RefreshToken.Trim());

        var tokenEntity = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (tokenEntity != null && !tokenEntity.IsRevoked)
        {
            tokenEntity.IsRevoked = true;
            tokenEntity.RevokedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_auditService != null)
            {
                await _auditService.LogAsync(
                    SecurityAuditEventTypes.Logout,
                    nameof(RefreshToken),
                    tokenEntity.RefreshTokenId.ToString(),
                    oldValues: null,
                    newValues: new { tokenEntity.UserId },
                    userId: tokenEntity.UserId,
                    cancellationToken: cancellationToken);
            }
        }

        return new LogoutResponseDto(
            Success: true,
            Message: "Logged out successfully."
        );
    }

    public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user != null && !user.IsDeleted)
        {
            var rawResetToken = GenerateSecureToken("reset", user.UserId);
            var resetTokenHash = HashString(rawResetToken);

            var verification = new EmailVerification
            {
                UserId = user.UserId,
                TokenHash = resetTokenHash,
                Method = "ResetLink",
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.EmailVerifications.Add(verification);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _emailService.SendPasswordResetEmailAsync(user.Email, user.Name, rawResetToken, cancellationToken);
        }

        return new ForgotPasswordResponseDto(
            Success: true,
            Message: "If the email is registered, a password reset link has been sent to your email address."
        );
    }

    public async Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
        {
            throw new ArgumentException("Reset token is required.", nameof(dto.Token));
        }

        var tokenToVerify = dto.Token.Trim();
        if (tokenToVerify.Contains("token="))
        {
            var idx = tokenToVerify.IndexOf("token=");
            tokenToVerify = tokenToVerify.Substring(idx + 6);
            var ampIdx = tokenToVerify.IndexOf('&');
            if (ampIdx >= 0)
            {
                tokenToVerify = tokenToVerify.Substring(0, ampIdx);
            }
        }
        tokenToVerify = Uri.UnescapeDataString(tokenToVerify);

        var tokenHash = HashString(tokenToVerify);

        var verification = await _dbContext.EmailVerifications
            .Include(v => v.User)
            .Where(v => v.TokenHash == tokenHash && v.Method == "ResetLink" && v.VerifiedAt == null && v.ExpiresAt > DateTime.UtcNow && v.Attempts < 5)
            .FirstOrDefaultAsync(cancellationToken);

        if (verification == null || verification.User == null || verification.User.IsDeleted)
        {
            throw new InvalidOperationException("Invalid or expired reset token.");
        }

        var newPasswordHash = _passwordHasher.HashPassword(verification.User, dto.NewPassword);
        verification.User.PasswordHash = newPasswordHash;
        verification.VerifiedAt = DateTime.UtcNow;

        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == verification.UserId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var rt in activeTokens)
        {
            rt.IsRevoked = true;
            rt.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SecurityAuditEventTypes.PasswordReset,
                nameof(User),
                verification.UserId.ToString(),
                oldValues: null,
                newValues: new { verification.UserId, verification.User.Email },
                userId: verification.UserId,
                cancellationToken: cancellationToken);
        }

        return new ResetPasswordResponseDto(
            Success: true,
            Message: "Password reset successfully. You can now log in with your new password."
        );
    }

    private static string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static string GenerateSecureToken(string type, int userId)
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var base64 = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
        return $"{type}_{userId}_{base64}";
    }
}
