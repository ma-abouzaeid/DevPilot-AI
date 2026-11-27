using System.Net;
using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevPilotAI.Infrastructure.Email;

public class BrevoEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly BrevoOptions _brevoOptions;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(
        HttpClient httpClient,
        IOptions<BrevoOptions> brevoOptions,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<BrevoEmailService> logger)
    {
        _httpClient = httpClient;
        _brevoOptions = brevoOptions.Value;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string recipientEmail, string recipientName, string otpCode, CancellationToken cancellationToken = default)
    {
        var subject = "Verify your DevPilot AI account";
        var templateContent = LoadTemplate("VerificationEmail.html");

        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipientName) ? "Developer" : recipientName);
        var safeOtp = WebUtility.HtmlEncode(otpCode);

        var htmlBody = templateContent
            .Replace("{{RecipientName}}", safeName)
            .Replace("{{OtpCode}}", safeOtp);

        await SendTransactionalEmailAsync(recipientEmail, safeName, subject, htmlBody, cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string recipientEmail, string recipientName, string resetToken, CancellationToken cancellationToken = default)
    {
        var subject = "Reset your DevPilot AI password";
        var templateContent = LoadTemplate("PasswordResetEmail.html");

        var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipientName) ? "Developer" : recipientName);
        var frontendBaseUrl = string.IsNullOrWhiteSpace(_frontendOptions.BaseUrl) ? "https://devpilot.ai" : _frontendOptions.BaseUrl.TrimEnd('/');
        var resetUrl = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";
        var safeResetUrl = WebUtility.HtmlEncode(resetUrl);

        var htmlBody = templateContent
            .Replace("{{RecipientName}}", safeName)
            .Replace("{{ResetUrl}}", safeResetUrl);

        await SendTransactionalEmailAsync(recipientEmail, safeName, subject, htmlBody, cancellationToken);
    }

    private async Task SendTransactionalEmailAsync(string recipientEmail, string recipientName, string subject, string htmlContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_brevoOptions.ApiKey))
        {
            _logger.LogWarning("Brevo API key is not configured. Email to {RecipientEmail} was skipped.", recipientEmail);
            return;
        }

        var payload = new
        {
            sender = new
            {
                name = _brevoOptions.SenderName,
                email = _brevoOptions.SenderEmail
            },
            to = new[]
            {
                new
                {
                    email = recipientEmail,
                    name = recipientName
                }
            },
            subject = subject,
            htmlContent = htmlContent
        };

        var requestJson = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v3/smtp/email");
        request.Headers.Add("api-key", _brevoOptions.ApiKey);
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Brevo transactional email delivery failed with status code {StatusCode}.", (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to communicate with Brevo Transactional Email API for {RecipientEmail}.", recipientEmail);
        }
    }

    private string LoadTemplate(string templateFileName)
    {
        try
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "Email", "Templates", templateFileName);
            if (File.Exists(templatePath))
            {
                return File.ReadAllText(templatePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read email template file {TemplateName}. Using default template layout.", templateFileName);
        }

        return GetFallbackTemplate(templateFileName);
    }

    private static string GetFallbackTemplate(string templateFileName)
    {
        if (templateFileName.Contains("PasswordReset", StringComparison.OrdinalIgnoreCase))
        {
            return "<html><body style=\"margin:0;background:#F4F6F8;font-family:sans-serif;color:#0B1F33;\">" +
                   "<div style=\"max-width:560px;margin:32px auto;background:#fff;border:1px solid #E2E8F0;border-radius:16px;padding:28px;\">" +
                   "<h2 style=\"color:#0F766E;margin:0 0 12px;\">DevPilot AI</h2>" +
                   "<h3 style=\"margin:0 0 12px;\">Reset your password</h3>" +
                   "<p>Hello {{RecipientName}},</p>" +
                   "<p><a href=\"{{ResetUrl}}\" style=\"display:inline-block;background:#0F766E;color:#fff;text-decoration:none;padding:12px 20px;border-radius:10px;\">Reset Password</a></p>" +
                   "<p style=\"color:#64748B;\">This link expires in 30 minutes.</p></div></body></html>";
        }

        return "<html><body style=\"margin:0;background:#F4F6F8;font-family:sans-serif;color:#0B1F33;\">" +
               "<div style=\"max-width:560px;margin:32px auto;background:#fff;border:1px solid #E2E8F0;border-radius:16px;padding:28px;\">" +
               "<h2 style=\"color:#0F766E;margin:0 0 12px;\">DevPilot AI</h2>" +
               "<h3 style=\"margin:0 0 12px;\">Verify your email</h3>" +
               "<p>Hello {{RecipientName}},</p>" +
               "<p style=\"font-size:28px;letter-spacing:0.25em;color:#0F766E;font-weight:700;\">{{OtpCode}}</p>" +
               "<p style=\"color:#64748B;\">This code expires in 15 minutes.</p></div></body></html>";
    }
}
