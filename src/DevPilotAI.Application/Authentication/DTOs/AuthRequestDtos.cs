using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Authentication.DTOs;

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

    [StringLength(2048, ErrorMessage = "Turnstile token cannot exceed 2048 characters.")]
    public string? TurnstileToken { get; set; }
}

public class VerifyEmailRequestDto
{
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage = "Verification code cannot exceed 20 characters.")]
    public string? Code { get; set; }

    [StringLength(2048, ErrorMessage = "Verification token cannot exceed 2048 characters.")]
    public string? Token { get; set; }
}

public class ResendVerificationRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;
}

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Password cannot exceed 100 characters.")]
    public string Password { get; set; } = string.Empty;

    public bool? RememberMe { get; set; }

    [StringLength(2048, ErrorMessage = "Turnstile token cannot exceed 2048 characters.")]
    public string? TurnstileToken { get; set; }
}

public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token is required.")]
    [StringLength(512, MinimumLength = 10, ErrorMessage = "Invalid refresh token format.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class LogoutRequestDto
{
    [Required(ErrorMessage = "Refresh token is required.")]
    [StringLength(512, MinimumLength = 10, ErrorMessage = "Invalid refresh token format.")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequestDto
{
    [Required(ErrorMessage = "Reset token is required.")]
    [StringLength(2048, MinimumLength = 10, ErrorMessage = "Invalid reset token format.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public string NewPassword { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string? Email { get; set; }
}
