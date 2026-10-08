namespace DevPilotAI.Infrastructure.Email;

public class BrevoOptions
{
    public const string SectionName = "Brevo";

    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = "midohema94@gmail.com";
    public string SenderName { get; set; } = "DevPilot";
    public string BaseUrl { get; set; } = "https://api.brevo.com";
}
