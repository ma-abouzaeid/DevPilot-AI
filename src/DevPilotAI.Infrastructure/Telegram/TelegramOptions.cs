namespace DevPilotAI.Infrastructure.Telegram;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    /// <summary>Optional secret checked via X-Telegram-Bot-Api-Secret-Token.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
}
