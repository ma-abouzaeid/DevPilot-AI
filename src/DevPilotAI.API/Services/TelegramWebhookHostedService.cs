using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Telegram;
using DevPilotAI.Infrastructure.Telegram;
using Microsoft.Extensions.Options;

namespace DevPilotAI.API.Services;

/// <summary>
/// Telegram ingress: HTTPS public URL → setWebhook; otherwise deleteWebhook + getUpdates long polling
/// (Telegram rejects HTTP webhook URLs).
/// </summary>
public sealed class TelegramWebhookHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<TelegramOptions> _options;
    private readonly ILogger<TelegramWebhookHostedService> _logger;

    public TelegramWebhookHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramOptions> options,
        ILogger<TelegramWebhookHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        if (string.IsNullOrWhiteSpace(opts.BotToken))
        {
            _logger.LogWarning(
                "Telegram BotToken is empty. Smart Support bot will not reply until Telegram__BotToken is set on the host.");
            return;
        }

        if (opts.ShouldUseLongPolling())
        {
            await RunLongPollingAsync(stoppingToken);
            return;
        }

        await RegisterWebhookAsync(stoppingToken);
    }

    private async Task RegisterWebhookAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        if (!opts.AutoSetWebhook)
        {
            _logger.LogInformation("Telegram AutoSetWebhook is disabled.");
            return;
        }

        var baseUrl = (opts.PublicApiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("Telegram PublicApiBaseUrl is empty; cannot register webhook.");
            return;
        }

        if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Telegram PublicApiBaseUrl must be HTTPS for webhook mode. Falling back to long polling. Current value: {BaseUrl}",
                baseUrl);
            await RunLongPollingAsync(cancellationToken);
            return;
        }

        var webhookUrl = baseUrl + "/api/Telegram/webhook";

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var (ok, detail) = await bot.SetWebhookAsync(webhookUrl, opts.WebhookSecret, cancellationToken);
            if (ok)
                _logger.LogInformation("Telegram webhook registered: {WebhookUrl}", webhookUrl);
            else
                _logger.LogError("Telegram setWebhook failed for {WebhookUrl}: {Detail}", webhookUrl, detail);

            var info = await bot.GetWebhookInfoAsync(cancellationToken);
            _logger.LogInformation("Telegram getWebhookInfo: {Info}", info.Detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook registration threw on startup.");
        }
    }

    private async Task RunLongPollingAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Telegram long polling enabled (HTTP/public webhook unavailable). Deleting any existing webhook…");

        try
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
                var (ok, detail) = await bot.DeleteWebhookAsync(dropPendingUpdates: true, stoppingToken);
                if (ok)
                    _logger.LogInformation("Telegram webhook cleared for long polling.");
                else
                    _logger.LogWarning("Telegram deleteWebhook failed: {Detail}", detail);
            }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Telegram deleteWebhook threw; continuing with getUpdates.");
        }

        long offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool ok;
                IReadOnlyList<System.Text.Json.JsonElement> updates;
                string detail;
                await using (var pollScope = _scopeFactory.CreateAsyncScope())
                {
                    var botClient = pollScope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
                    (ok, updates, detail) = await botClient.GetUpdatesAsync(offset, timeoutSeconds: 50, stoppingToken);
                }

                if (!ok)
                {
                    _logger.LogWarning("Telegram getUpdates failed: {Detail}", detail);
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                foreach (var update in updates)
                {
                    var updateId = update.TryGetProperty("update_id", out var idEl) ? idEl.GetInt64() : 0;
                    if (updateId >= offset)
                        offset = updateId + 1;

                    try
                    {
                        await using var handleScope = _scopeFactory.CreateAsyncScope();
                        var botService = handleScope.ServiceProvider.GetRequiredService<ITelegramBotService>();
                        await botService.HandleUpdateAsync(update, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Telegram update {UpdateId} handling failed.", updateId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram long polling loop error; retrying…");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Telegram long polling stopped.");
    }
}
