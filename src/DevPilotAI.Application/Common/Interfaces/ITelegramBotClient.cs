namespace DevPilotAI.Application.Common.Interfaces;

public record TelegramOutgoingMessage(
    long ChatId,
    string Text,
    string? ReplyMarkupJson = null
);

public record TelegramDownloadedFile(
    byte[] Bytes,
    string? FilePath,
    string? MimeType
);

public interface ITelegramBotClient
{
    bool IsConfigured { get; }
    Task SendMessageAsync(TelegramOutgoingMessage message, CancellationToken cancellationToken = default);
    Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default);
    Task<TelegramDownloadedFile?> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default);
    Task<(bool Ok, string Detail)> SetWebhookAsync(
        string webhookUrl,
        string? secretToken = null,
        CancellationToken cancellationToken = default);
    Task<(bool Ok, string Detail)> DeleteWebhookAsync(
        bool dropPendingUpdates = true,
        CancellationToken cancellationToken = default);
    Task<(bool Ok, string Detail)> GetWebhookInfoAsync(CancellationToken cancellationToken = default);
    Task<(bool Ok, IReadOnlyList<System.Text.Json.JsonElement> Updates, string Detail)> GetUpdatesAsync(
        long offset,
        int timeoutSeconds = 50,
        CancellationToken cancellationToken = default);
}
