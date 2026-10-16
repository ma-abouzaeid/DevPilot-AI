using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Services;

public interface IUserService
{
    Task DeleteAccountAsync(int userId, CancellationToken cancellationToken = default);

    // Retrieve the current authenticated user's profile (safe fields only)
    Task<UserDto> GetCurrentProfileAsync(int userId, CancellationToken cancellationToken = default);

    // Update mutable profile fields (name only). Returns the updated profile DTO.
    Task<UserDto> UpdateCurrentProfileAsync(int userId, UpdateUserProfileRequestDto request, CancellationToken cancellationToken = default);
}

// DTO used for profile updates – validates only the Name field according to existing conventions.
public record UpdateUserProfileRequestDto(
    [Required]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 150 characters.")]
    string Name
);
