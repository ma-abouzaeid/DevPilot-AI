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
    /// Force getUpdates long polling. When null, auto: poll if base URL is not HTTPS.
    /// </summary>
    public bool? UseLongPolling { get; set; }

    public bool ShouldUseLongPolling()
    {
        if (UseLongPolling.HasValue)
            return UseLongPolling.Value;

        var baseUrl = (PublicApiBaseUrl ?? string.Empty).Trim();
        return !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
