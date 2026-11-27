namespace DevPilotAI.Infrastructure.Telegram;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    // X-Telegram-Bot-Api-Secret-Token header (webhook mode only)
    public string WebhookSecret { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;

    /// <summary>
    /// Public API base. HTTPS → webhook mode; HTTP → long polling (Telegram rejects HTTP webhooks).
    /// </summary>
    public string PublicApiBaseUrl { get; set; } = "http://devpilot-ai.runasp.net";

    /// <summary>
    /// When true and PublicApiBaseUrl is HTTPS, register webhook on start.
    /// Ignored for HTTP bases (long polling is used instead).
    /// </summary>
    public bool AutoSetWebhook { get; set; } = true;

    /// <summary>
    /// Use getUpdates long polling (required when public HTTPS webhook is unavailable).
    /// Defaults to true so HTTP hosts keep working without a webhook.
    /// </summary>
    public bool UseLongPolling { get; set; } = true;

    public bool ShouldUseLongPolling() => UseLongPolling;
}
