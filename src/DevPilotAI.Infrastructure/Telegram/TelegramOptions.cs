namespace DevPilotAI.Infrastructure.Telegram;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    // X-Telegram-Bot-Api-Secret-Token header
    public string WebhookSecret { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
}
