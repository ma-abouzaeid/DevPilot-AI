using System.Security.Claims;
using DevPilotAI.Application.Services;
using DevPilotAI.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpDelete("me/Delete")]
    public async Task<IActionResult> DeleteCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                errorCode = "UNAUTHORIZED",
                message = "Invalid or missing user identity in access token."
            });
        }

        await _userService.DeleteAccountAsync(userId, cancellationToken);

        return Ok(new
        {
            success = true,
            message = "User account has been successfully deleted."
        });
    }


    // GET current user profile
    [HttpGet("me/Profile")]
    public async Task<IActionResult> GetCurrentProfile(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                errorCode = "UNAUTHORIZED",
                message = "Invalid or missing user identity in access token."
            });
        }

        var profile = await _userService.GetCurrentProfileAsync(userId, cancellationToken);
        return Ok(new ApiResponse<UserDto>
        {
            success = true,
            data = profile
        });
    }

    // PUT update current user profile (name only)
    [HttpPut("me/Update/Profile")]
    public async Task<IActionResult> UpdateCurrentProfile([FromBody] UpdateUserProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new
            {
                success = false,
                errorCode = "UNAUTHORIZED",
                message = "Invalid or missing user identity in access token."
            });
        }

        var updated = await _userService.UpdateCurrentProfileAsync(userId, request, cancellationToken);
        return Ok(new ApiResponse<UserDto>
        {
            success = true,
            data = updated
        });
    }

}
