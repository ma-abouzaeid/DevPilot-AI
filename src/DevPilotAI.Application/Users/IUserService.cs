using System.ComponentModel.DataAnnotations;
using DevPilotAI.Application.Authentication.DTOs;

namespace DevPilotAI.Application.Users;

public interface IUserService
{
    Task DeleteAccountAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserDto> GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserDto> UpdateCurrentProfileAsync(int userId, UpdateUserProfileRequestDto request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<EmailChangeResponseDto> RequestEmailChangeAsync(int userId, RequestEmailChangeDto request, CancellationToken cancellationToken = default);
    Task<UserDto> ConfirmEmailChangeAsync(int userId, ConfirmEmailChangeDto request, CancellationToken cancellationToken = default);
    Task<EmailChangeResponseDto> ResendEmailChangeAsync(int userId, CancellationToken cancellationToken = default);
}

public record UpdateUserProfileRequestDto(
    [Required]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters.")]
    string Name
);

public class ChangePasswordRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword), ErrorMessage = "New password and confirmation do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public record RequestEmailChangeDto(
    [Required]
    [StringLength(100, MinimumLength = 1)]
    string CurrentPassword,

    [Required]
    [EmailAddress]
    [StringLength(255)]
    string NewEmail
);

public record ConfirmEmailChangeDto(
    [Required]
    [StringLength(10, MinimumLength = 6)]
    string Code
);

public record EmailChangeResponseDto(
    bool Success,
    string Message,
    string PendingEmail
);
