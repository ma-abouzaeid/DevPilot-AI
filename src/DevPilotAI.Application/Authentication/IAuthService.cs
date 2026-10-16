using DevPilotAI.Application.Authentication.DTOs;

namespace DevPilotAI.Application.Authentication;

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
