namespace DevPilotAI.Infrastructure.Telegram;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    // X-Telegram-Bot-Api-Secret-Token header
    public string WebhookSecret { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;

    /// <summary>
    /// Public HTTPS base of the API (e.g. https://devpilot-ai.runasp.net).
    /// Used at startup to call Telegram setWebhook → {PublicApiBaseUrl}/api/Telegram/webhook
    /// </summary>
    public string PublicApiBaseUrl { get; set; } = "https://devpilot-ai.runasp.net";

    /// <summary>When true (default), register the webhook with Telegram on app start.</summary>
    public bool AutoSetWebhook { get; set; } = true;
}
