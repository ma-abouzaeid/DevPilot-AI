using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Authentication;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(dto, cancellationToken);
        var response = ApiResponse<RegisterResponseDto>.SuccessResult(result, "Your account has been created successfully. A verification code has been sent to your email.", GetTraceId());
        return Created(string.Empty, response);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyEmailAsync(dto, cancellationToken);
        var response = ApiResponse<VerifyEmailResponseDto>.SuccessResult(result, "Your email address has been verified successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.ResendVerificationAsync(dto, cancellationToken);
        var response = ApiResponse<ResendVerificationResponseDto>.SuccessResult(result, "If eligible, a new verification code has been sent to your email.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(dto, cancellationToken);
        var response = ApiResponse<LoginResponseDto>.SuccessResult(result, "You have logged in successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(dto, cancellationToken);
        var response = ApiResponse<RefreshTokenResponseDto>.SuccessResult(result, "Token refreshed successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.LogoutAsync(dto, cancellationToken);
        var response = ApiResponse<LogoutResponseDto>.SuccessResult(result, "You have been logged out successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.ForgotPasswordAsync(dto, cancellationToken);
        var response = ApiResponse<ForgotPasswordResponseDto>.SuccessResult(result, result.Message, GetTraceId());
        return Ok(response);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(dto, cancellationToken);
        var response = ApiResponse<ResetPasswordResponseDto>.SuccessResult(result, "Your password has been reset successfully. You can now log in.", GetTraceId());
        return Ok(response);
    }
}
