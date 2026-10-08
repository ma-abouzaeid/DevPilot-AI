using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Security;

public class TurnstileService : ITurnstileService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TurnstileService> _logger;

    public TurnstileService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TurnstileService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> VerifyTokenAsync(string? token, string? remoteIp = null, CancellationToken cancellationToken = default)
    {
        var enabled = _configuration.GetValue<bool>("Turnstile:Enabled", false);
        if (!enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var secretKey = _configuration["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogWarning("Turnstile is enabled but secret key is not configured.");
            return true;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey),
                new KeyValuePair<string, string>("response", token),
                new KeyValuePair<string, string>("remoteip", remoteIp ?? string.Empty)
            });

            var response = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);
            return result?.Success == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify Turnstile token with Cloudflare API.");
            return false;
        }
    }

    private class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
