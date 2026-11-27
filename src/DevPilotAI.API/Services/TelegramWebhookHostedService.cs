using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Infrastructure.Telegram;
using Microsoft.Extensions.Options;

namespace DevPilotAI.API.Services;

/// <summary>
/// Registers the Telegram webhook on startup so the bot receives /start and other updates.
/// </summary>
public sealed class TelegramWebhookHostedService : IHostedService
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        if (!opts.AutoSetWebhook)
        {
            _logger.LogInformation("Telegram AutoSetWebhook is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(opts.BotToken))
        {
            _logger.LogWarning(
                "Telegram BotToken is empty. Smart Support bot will not reply until Telegram__BotToken is set on the host.");
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
                "Telegram PublicApiBaseUrl must be HTTPS (Telegram rejects HTTP webhooks). Current value: {BaseUrl}",
                baseUrl);
            return;
        }

        var webhookUrl = baseUrl + "/api/Telegram/webhook";

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var (ok, detail) = await bot.SetWebhookAsync(webhookUrl, opts.WebhookSecret, cancellationToken);
            if (ok)
            {
                _logger.LogInformation("Telegram webhook registered: {WebhookUrl}", webhookUrl);
            }
            else
            {
                _logger.LogError("Telegram setWebhook failed for {WebhookUrl}: {Detail}", webhookUrl, detail);
            }

            var info = await bot.GetWebhookInfoAsync(cancellationToken);
            _logger.LogInformation("Telegram getWebhookInfo: {Info}", info.Detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram webhook registration threw on startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
