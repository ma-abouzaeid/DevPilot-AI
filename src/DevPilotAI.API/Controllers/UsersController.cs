using System.Security.Claims;
using DevPilotAI.Application.Services;
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

    [HttpDelete("me")]
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
}
