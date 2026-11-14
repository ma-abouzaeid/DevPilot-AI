using System.Security.Claims;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Users;
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

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private int GetRequiredUserId()
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId) || userId <= 0)
        {
            throw AppError.Unauthorized("Invalid or missing user identity in access token.");
        }
        return userId;
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteCurrentUser(CancellationToken cancellationToken)
    {
        await _userService.DeleteAccountAsync(GetRequiredUserId(), cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Your account has been deactivated successfully.", GetTraceId()));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentProfile(CancellationToken cancellationToken)
    {
        var profile = await _userService.GetCurrentProfileAsync(GetRequiredUserId(), cancellationToken);
        return Ok(ApiResponse<UserDto>.SuccessResult(profile, "User profile retrieved successfully.", GetTraceId()));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrentProfile([FromBody] UpdateUserProfileRequestDto request, CancellationToken cancellationToken)
    {
        var updated = await _userService.UpdateCurrentProfileAsync(GetRequiredUserId(), request, cancellationToken);
        return Ok(ApiResponse<UserDto>.SuccessResult(updated, "User profile updated successfully.", GetTraceId()));
    }

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _userService.ChangePasswordAsync(GetRequiredUserId(), request, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Password changed successfully. Please sign in again on other devices.", GetTraceId()));
    }
}
