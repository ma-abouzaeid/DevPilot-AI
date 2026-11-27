using System.Text.Json;
using DevPilotAI.API.Services;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Telegram;
using DevPilotAI.Infrastructure.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// Do not rate-limit Telegram webhooks — 429 makes Telegram stop delivering updates.
public class TelegramController : ControllerBase
{
    private readonly ITelegramBotService _bot;
    private readonly ITelegramBotClient _client;
    private readonly TelegramUpdatePump _pump;
    private readonly TelegramOptions _options;

    public TelegramController(
        ITelegramBotService bot,
        ITelegramBotClient client,
        TelegramUpdatePump pump,
        IOptions<TelegramOptions> options)
    {
        _bot = bot;
        _client = client;
        _pump = pump;
        _options = options.Value;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    /// <summary>Diagnostics + immediate getUpdates drain (no secrets).</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        string? getMe = null;
        var getMeOk = false;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(12));
            var me = await _client.GetMeAsync(cts.Token);
            getMeOk = me.Ok;
            getMe = me.Detail.Length > 300 ? me.Detail[..300] + "…" : me.Detail;
        }
        catch (Exception ex)
        {
            getMe = ex.GetType().Name + ": " + ex.Message;
        }

        int processed = 0;
        string pollDetail = "skipped";
        bool pollOk = false;
        if (_options.UseLongPolling && _client.IsConfigured)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(15));
                var poll = await _pump.PollOnceAsync(timeoutSeconds: 0, cts.Token);
                pollOk = poll.Ok;
                processed = poll.Processed;
                pollDetail = poll.Detail;
            }
            catch (Exception ex)
            {
                pollDetail = ex.GetType().Name + ": " + ex.Message;
            }
        }

        return Ok(new
        {
            configured = _client.IsConfigured,
            useLongPolling = _options.UseLongPolling,
            publicApiBaseUrl = _options.PublicApiBaseUrl,
            botUsername = string.IsNullOrWhiteSpace(_options.BotUsername) ? null : _options.BotUsername,
            mode = _options.UseLongPolling ? "long-polling" : "webhook",
            loopAlive = _pump.LoopAlive,
            lastPollUtc = _pump.LastPollUtc,
            lastPollOk = _pump.LastPollOk,
            lastUpdateCount = _pump.LastUpdateCount,
            lastError = _pump.LastError,
            offset = _pump.Offset,
            getMeOk,
            getMe,
            drainOk = pollOk,
            drainProcessed = processed,
            drainDetail = pollDetail
        });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            var header = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
            if (!string.Equals(header, _options.WebhookSecret, StringComparison.Ordinal))
            {
                return Unauthorized();
            }
        }

        using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        await _bot.HandleUpdateAsync(doc.RootElement.Clone(), cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "OK", GetTraceId()));
    }
}
