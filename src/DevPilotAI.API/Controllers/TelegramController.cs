using System.Text.Json;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Telegram;
using DevPilotAI.Infrastructure.Security;
using DevPilotAI.Infrastructure.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.General)]
public class TelegramController : ControllerBase
{
    private readonly ITelegramBotService _bot;
    private readonly TelegramOptions _options;

    public TelegramController(ITelegramBotService bot, IOptions<TelegramOptions> options)
    {
        _bot = bot;
        _options = options.Value;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

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
