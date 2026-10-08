using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Services;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public string Password { get; set; } = string.Empty;

    public string? TurnstileToken { get; set; }
}

public class VerifyEmailRequestDto
{
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage = "Verification code cannot exceed 20 characters.")]
    public string? Code { get; set; }

    public string? Token { get; set; }
}

public class ResendVerificationRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;
}

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequestDto
{
    [Required(ErrorMessage = "Refresh token is required.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequestDto
{
    [Required(ErrorMessage = "Reset token is required.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public string NewPassword { get; set; } = string.Empty;
}

public record UserDto(
    int UserId,
    string Name,
    string Email,
    DateTime? EmailVerifiedAt
);

public record RegisterResponseDto(
    bool Success,
    string Message,
    string Email,
    bool RequiresVerification
);

public record VerifyEmailResponseDto(
    bool Success,
    string Message
);

public record ResendVerificationResponseDto(
    bool Success,
    string Message
);

public record LoginResponseDto(
    bool Success,
    string AccessToken,
    string RefreshToken,
    UserDto User
);

public record RefreshTokenResponseDto(
    bool Success,
    string AccessToken,
    string RefreshToken
);

public record LogoutResponseDto(
    bool Success,
    string Message
);

public record ForgotPasswordResponseDto(
    bool Success,
    string Message
);

public record ResetPasswordResponseDto(
    bool Success,
    string Message
);

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);
    Task<VerifyEmailResponseDto> VerifyEmailAsync(VerifyEmailRequestDto dto, CancellationToken cancellationToken = default);
    Task<ResendVerificationResponseDto> ResendVerificationAsync(ResendVerificationRequestDto dto, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
    Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default);
    Task<LogoutResponseDto> LogoutAsync(LogoutRequestDto dto, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken cancellationToken = default);
    Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default);
}
