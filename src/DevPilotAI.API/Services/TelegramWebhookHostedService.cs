using DevPilotAI.Infrastructure.Telegram;
using Microsoft.Extensions.Options;

namespace DevPilotAI.API.Services;

/// <summary>
/// Telegram ingress: HTTPS public URL → setWebhook; otherwise getUpdates long polling
/// (Telegram rejects HTTP webhook URLs). Shared hosting may unload the process; request
/// drains via <see cref="TelegramUpdatePump"/> keep the bot responsive while awake.
/// </summary>
public sealed class TelegramWebhookHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramUpdatePump _pump;
    private readonly IOptions<TelegramOptions> _options;
    private readonly ILogger<TelegramWebhookHostedService> _logger;

    public TelegramWebhookHostedService(
        IServiceScopeFactory scopeFactory,
        TelegramUpdatePump pump,
        IOptions<TelegramOptions> options,
        ILogger<TelegramWebhookHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _pump = pump;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var opts = _options.Value;
        if (string.IsNullOrWhiteSpace(opts.BotToken))
        {
            _logger.LogWarning(
                "Telegram BotToken is empty. Smart Support bot will not reply until Telegram__BotToken is set on the host.");
            return;
        }

        _logger.LogInformation(
            "Telegram ingress starting. UseLongPolling={UseLongPolling}, PublicApiBaseUrl={BaseUrl}",
            opts.UseLongPolling,
            opts.PublicApiBaseUrl);

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
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Telegram webhook mode requires HTTPS PublicApiBaseUrl. Falling back to long polling. Current value: {BaseUrl}",
                baseUrl);
            await RunLongPollingAsync(cancellationToken);
            return;
        }

        var webhookUrl = baseUrl + "/api/Telegram/webhook";

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var bot = scope.ServiceProvider.GetRequiredService<DevPilotAI.Application.Common.Interfaces.ITelegramBotClient>();
            var (ok, detail) = await bot.SetWebhookAsync(webhookUrl, opts.WebhookSecret, cancellationToken);
            if (ok)
                _logger.LogInformation("Telegram webhook registered: {WebhookUrl}", webhookUrl);
            else
                _logger.LogError("Telegram setWebhook failed for {WebhookUrl}: {Detail}", webhookUrl, detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook registration threw on startup.");
        }
    }

    private async Task RunLongPollingAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram long polling loop starting.");
        _pump.LoopAlive = true;

        try
        {
            await _pump.EnsureWebhookClearedAsync(stoppingToken);

            // Short polls survive MonsterASP/IIS idle kills better than 50s holds.
            while (!stoppingToken.IsCancellationRequested)
            {
                var (ok, processed, detail) = await _pump.PollOnceAsync(timeoutSeconds: 20, stoppingToken);
                if (!ok)
                {
                    _logger.LogWarning("Telegram getUpdates failed: {Detail}", detail);
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                else if (processed > 0)
                {
                    _logger.LogInformation("Telegram processed {Count} update(s).", processed);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shut down
        }
        finally
        {
            _pump.LoopAlive = false;
            _logger.LogInformation("Telegram long polling stopped.");
        }
    }
}
