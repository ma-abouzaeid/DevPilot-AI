using System.Text.Json;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Telegram;
using DevPilotAI.Infrastructure.Telegram;
using Microsoft.Extensions.Options;

namespace DevPilotAI.API.Services;

/// <summary>
/// Shared getUpdates pump for BackgroundService + request-triggered drains (MonsterASP idle unload).
/// </summary>
public sealed class TelegramUpdatePump
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<TelegramOptions> _options;
    private readonly ILogger<TelegramUpdatePump> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _offset;
    private bool _webhookCleared;

    public TelegramUpdatePump(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramOptions> options,
        ILogger<TelegramUpdatePump> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public bool LoopAlive { get; set; }
    public DateTimeOffset? LastPollUtc { get; private set; }
    public bool LastPollOk { get; private set; }
    public int LastUpdateCount { get; private set; }
    public string? LastError { get; private set; }
    public long Offset => Interlocked.Read(ref _offset);

    public async Task EnsureWebhookClearedAsync(CancellationToken cancellationToken = default)
    {
        if (_webhookCleared || string.IsNullOrWhiteSpace(_options.Value.BotToken))
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_webhookCleared) return;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var (ok, detail) = await bot.DeleteWebhookAsync(dropPendingUpdates: false, cancellationToken);
            if (ok)
            {
                _webhookCleared = true;
                _logger.LogInformation("Telegram webhook cleared for long polling.");
            }
            else
            {
                LastError = "deleteWebhook failed: " + Truncate(detail);
                _logger.LogWarning("Telegram deleteWebhook failed: {Detail}", detail);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<(bool Ok, int Processed, string Detail)> PollOnceAsync(
        int timeoutSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Value.BotToken))
            return (false, 0, "BotToken is not configured.");

        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken))
            return (false, 0, "Poll already in progress.");

        try
        {
            if (!_webhookCleared)
                await EnsureWebhookClearedUnlockedAsync(cancellationToken);

            await using var pollScope = _scopeFactory.CreateAsyncScope();
            var botClient = pollScope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var offset = Interlocked.Read(ref _offset);
            var (ok, updates, detail) = await botClient.GetUpdatesAsync(offset, timeoutSeconds, cancellationToken);

            LastPollUtc = DateTimeOffset.UtcNow;
            LastPollOk = ok;
            LastUpdateCount = updates.Count;

            if (!ok)
            {
                LastError = Truncate(detail);
                return (false, 0, detail);
            }

            LastError = null;
            var processed = 0;

            foreach (var update in updates)
            {
                var updateId = update.TryGetProperty("update_id", out var idEl) ? idEl.GetInt64() : 0;
                if (updateId >= offset)
                {
                    offset = updateId + 1;
                    Interlocked.Exchange(ref _offset, offset);
                }

                try
                {
                    await using var handleScope = _scopeFactory.CreateAsyncScope();
                    var botService = handleScope.ServiceProvider.GetRequiredService<ITelegramBotService>();
                    await botService.HandleUpdateAsync(update, cancellationToken);
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telegram update {UpdateId} handling failed.", updateId);
                    LastError = "handle failed: " + ex.Message;
                }
            }

            return (true, processed, $"processed={processed}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastPollUtc = DateTimeOffset.UtcNow;
            LastPollOk = false;
            LastError = ex.GetType().Name + ": " + ex.Message;
            _logger.LogError(ex, "Telegram poll failed.");
            return (false, 0, LastError);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureWebhookClearedUnlockedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
        var (ok, detail) = await bot.DeleteWebhookAsync(dropPendingUpdates: false, cancellationToken);
        if (ok)
        {
            _webhookCleared = true;
            _logger.LogInformation("Telegram webhook cleared for long polling.");
        }
        else
        {
            LastError = "deleteWebhook failed: " + Truncate(detail);
            _logger.LogWarning("Telegram deleteWebhook failed: {Detail}", detail);
        }
    }

    private static string Truncate(string? value, int max = 240)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = value.Trim();
        return value.Length <= max ? value : value[..max] + "…";
    }
}
