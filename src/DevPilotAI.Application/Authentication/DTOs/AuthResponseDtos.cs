namespace DevPilotAI.Application.Authentication.DTOs;

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
