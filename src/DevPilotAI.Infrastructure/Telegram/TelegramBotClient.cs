using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevPilotAI.Infrastructure.Telegram;

public class TelegramBotClient : ITelegramBotClient
{
    private readonly HttpClient _http;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramBotClient> _logger;

    public TelegramBotClient(
        HttpClient http,
        IOptions<TelegramOptions> options,
        ILogger<TelegramBotClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BotToken);

    public async Task SendMessageAsync(TelegramOutgoingMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return;

        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = message.ChatId,
            ["text"] = message.Text,
            ["parse_mode"] = "HTML"
        };

        if (!string.IsNullOrWhiteSpace(message.ReplyMarkupJson))
        {
            payload["reply_markup"] = JsonSerializer.Deserialize<JsonElement>(message.ReplyMarkupJson);
        }

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"bot{_options.BotToken}/sendMessage", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Telegram sendMessage failed ({Status}): {Body}", (int)response.StatusCode, body);
        }
    }

    public async Task AnswerCallbackAsync(string callbackQueryId, string? text = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(callbackQueryId)) return;

        var payload = new Dictionary<string, object?>
        {
            ["callback_query_id"] = callbackQueryId,
            ["text"] = text
        };
        await _http.PostAsJsonAsync($"bot{_options.BotToken}/answerCallbackQuery", payload, cancellationToken);
    }

    public async Task<TelegramDownloadedFile?> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(fileId)) return null;

        var metaRes = await _http.GetAsync($"bot{_options.BotToken}/getFile?file_id={Uri.EscapeDataString(fileId)}", cancellationToken);
        if (!metaRes.IsSuccessStatusCode) return null;

        using var metaDoc = JsonDocument.Parse(await metaRes.Content.ReadAsStringAsync(cancellationToken));
        if (!metaDoc.RootElement.TryGetProperty("result", out var result)) return null;
        var filePath = result.TryGetProperty("file_path", out var fp) ? fp.GetString() : null;
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        var fileRes = await _http.GetAsync($"file/bot{_options.BotToken}/{filePath}", cancellationToken);
        if (!fileRes.IsSuccessStatusCode) return null;

        var bytes = await fileRes.Content.ReadAsByteArrayAsync(cancellationToken);
        return new TelegramDownloadedFile(bytes, filePath, fileRes.Content.Headers.ContentType?.MediaType);
    }

    public async Task<(bool Ok, string Detail)> SetWebhookAsync(
        string webhookUrl,
        string? secretToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, "BotToken is not configured.");

        var payload = new Dictionary<string, object?>
        {
            ["url"] = webhookUrl,
            ["allowed_updates"] = new[] { "message", "callback_query" },
            ["drop_pending_updates"] = true
        };
        if (!string.IsNullOrWhiteSpace(secretToken))
            payload["secret_token"] = secretToken;

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"bot{_options.BotToken}/setWebhook", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, body);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var ok = doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
            return (ok, body);
        }
        catch
        {
            return (response.IsSuccessStatusCode, body);
        }
    }

    public async Task<(bool Ok, string Detail)> DeleteWebhookAsync(
        bool dropPendingUpdates = true,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, "BotToken is not configured.");

        var payload = new Dictionary<string, object?>
        {
            ["drop_pending_updates"] = dropPendingUpdates
        };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"bot{_options.BotToken}/deleteWebhook", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.IsSuccessStatusCode, body);
    }

    public async Task<(bool Ok, string Detail)> GetWebhookInfoAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, "BotToken is not configured.");

        var response = await _http.GetAsync($"bot{_options.BotToken}/getWebhookInfo", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.IsSuccessStatusCode, body);
    }

    public async Task<(bool Ok, IReadOnlyList<JsonElement> Updates, string Detail)> GetUpdatesAsync(
        long offset,
        int timeoutSeconds = 50,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, Array.Empty<JsonElement>(), "BotToken is not configured.");

        var payload = new Dictionary<string, object?>
        {
            ["offset"] = offset,
            ["timeout"] = Math.Clamp(timeoutSeconds, 0, 50),
            ["allowed_updates"] = new[] { "message", "callback_query" }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"bot{_options.BotToken}/getUpdates", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return (false, Array.Empty<JsonElement>(), body);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean();
            if (!ok || !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                return (false, Array.Empty<JsonElement>(), body);

            var updates = new List<JsonElement>(result.GetArrayLength());
            foreach (var item in result.EnumerateArray())
                updates.Add(item.Clone());

            return (true, updates, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Telegram getUpdates response.");
            return (false, Array.Empty<JsonElement>(), body);
        }
    }
}
