namespace DevPilotAI.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string recipientEmail, string recipientName, string otpCode, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetToken, CancellationToken cancellationToken = default);
}
