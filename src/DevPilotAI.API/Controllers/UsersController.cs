using System.Security.Claims;
using DevPilotAI.Application.Authentication.DTOs;
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

    [HttpDelete("me")]
    
    public async Task<IActionResult> DeleteCurrentUser(CancellationToken cancellationToken)
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            var traceId = GetTraceId();
            return Unauthorized(ApiResponse<object?>.FailureResult("Invalid or missing user identity in access token.", "UNAUTHORIZED", null, traceId));
        }

        await _userService.DeleteAccountAsync(userId, cancellationToken);

        var response = ApiResponse<object?>.SuccessResult(null, "Your account has been deactivated successfully.", GetTraceId());
        return Ok(response);
    }

    // GET current user profile
    [HttpGet("me")]
    
    public async Task<IActionResult> GetCurrentProfile(CancellationToken cancellationToken)
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            var traceId = GetTraceId();
            return Unauthorized(ApiResponse<object?>.FailureResult("Invalid or missing user identity in access token.", "UNAUTHORIZED", null, traceId));
        }

        var profile = await _userService.GetCurrentProfileAsync(userId, cancellationToken);
        var response = ApiResponse<UserDto>.SuccessResult(profile, "User profile retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    // PUT update current user profile (name only)
    [HttpPut("me")]
   
    public async Task<IActionResult> UpdateCurrentProfile([FromBody] UpdateUserProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            var traceId = GetTraceId();
            return Unauthorized(ApiResponse<object?>.FailureResult("Invalid or missing user identity in access token.", "UNAUTHORIZED", null, traceId));
        }

        var updated = await _userService.UpdateCurrentProfileAsync(userId, request, cancellationToken);
        var response = ApiResponse<UserDto>.SuccessResult(updated, "User profile updated successfully.", GetTraceId());
        return Ok(response);
    }
}
