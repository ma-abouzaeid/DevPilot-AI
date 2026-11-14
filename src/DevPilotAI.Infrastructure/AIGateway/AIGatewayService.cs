using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.AIGateway;

public class AIGatewayService : IAIGateway
{
    private readonly DevPilotDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIGatewayService> _logger;

    public AIGatewayService(
        DevPilotDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AIGatewayService> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AIGatewayResponse> ExecuteAsync(AIGatewayRequest request, CancellationToken cancellationToken = default)
    {
        var providers = await _dbContext.AIProviders
            .Include(p => p.Models)
            .Where(p => p.Status != ProviderStatus.Disabled)
            .ToListAsync(cancellationToken);

        providers = OrderProvidersForTask(providers, request.TaskType);

        if (providers.Count == 0)
        {
            return new AIGatewayResponse(
                IsSuccess: false,
                Content: string.Empty,
                ProviderName: "None",
                ModelName: "None",
                PromptTokens: 0,
                CompletionTokens: 0,
                DurationMs: 0,
                ErrorMessage: "No AI providers are configured. Seed providers and add API keys."
            );
        }

        var errors = new List<string>();

        foreach (var provider in providers)
        {
            if (provider.Status == ProviderStatus.RateLimited
                && provider.RateLimitedUntil.HasValue
                && provider.RateLimitedUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("Skipping rate-limited AI Provider {ProviderName} until {Until}", provider.Name, provider.RateLimitedUntil);
                continue;
            }

            var apiKey = ResolveApiKey(provider);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                errors.Add($"{provider.Name}: API key missing ({provider.ApiKeyConfigName})");
                continue;
            }

            var model = provider.Models.FirstOrDefault(m => m.IsActive)
                        ?? provider.Models.FirstOrDefault()
                        ?? new AIModel { ModelName = "default", AIProviderId = provider.AIProviderId };

            var sw = Stopwatch.StartNew();
            try
            {
                var responseContent = await CallProviderApiAsync(provider, model, request, apiKey, cancellationToken);
                sw.Stop();

                if (provider.Status == ProviderStatus.RateLimited)
                {
                    provider.Status = ProviderStatus.Active;
                    provider.RateLimitedUntil = null;
                    provider.LastError = null;
                }

                var promptTokens = EstimateTokens(request.SystemPrompt) + EstimateTokens(request.UserPrompt);
                var completionTokens = EstimateTokens(responseContent);

                var aiReq = new AIRequest
                {
                    UserId = request.UserId,
                    AIProviderId = provider.AIProviderId,
                    AIModelId = model.AIModelId > 0 ? model.AIModelId : 0,
                    DebuggingAttemptId = request.DebuggingAttemptId,
                    TaskType = request.TaskType,
                    PromptTokensUsed = promptTokens,
                    CompletionTokensUsed = completionTokens,
                    TotalTokensUsed = promptTokens + completionTokens,
                    DurationMs = sw.ElapsedMilliseconds,
                    Success = true
                };

                if (aiReq.AIModelId > 0)
                {
                    _dbContext.AIRequests.Add(aiReq);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                return new AIGatewayResponse(
                    IsSuccess: true,
                    Content: responseContent,
                    ProviderName: provider.Name,
                    ModelName: model.ModelName,
                    PromptTokens: promptTokens,
                    CompletionTokens: completionTokens,
                    DurationMs: sw.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "AI Provider {ProviderName} failed for task {TaskType}", provider.Name, request.TaskType);

                var isRateLimited = ex.Message.Contains("429", StringComparison.Ordinal)
                                    || ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase)
                                    || ex.Message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase);

                provider.Status = isRateLimited ? ProviderStatus.RateLimited : ProviderStatus.Active;
                provider.RateLimitedUntil = isRateLimited ? DateTime.UtcNow.AddMinutes(5) : null;
                provider.LastError = Truncate(ex.Message, 1000);
                errors.Add($"{provider.Name}: {Truncate(ex.Message, 300)}");

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return new AIGatewayResponse(
            IsSuccess: false,
            Content: string.Empty,
            ProviderName: "None",
            ModelName: "None",
            PromptTokens: 0,
            CompletionTokens: 0,
            DurationMs: 0,
            ErrorMessage: $"All AI Providers failed: {string.Join(" | ", errors)}"
        );
    }

    private static List<AIProvider> OrderProvidersForTask(List<AIProvider> providers, string taskType)
    {
        // KnowledgeAdaptation prefers Groq (fast). Debugging prefers Gemini (structured quality).
        int Rank(AIProvider p)
        {
            var name = p.Name.Trim();
            if (string.Equals(taskType, "KnowledgeAdaptation", StringComparison.OrdinalIgnoreCase))
            {
                return name switch
                {
                    "Groq" => 0,
                    "Gemini" => 1,
                    "NVIDIA" => 2,
                    "OpenRouter" => 3,
                    _ => 50 + p.Priority
                };
            }

            return name switch
            {
                "Gemini" => 0,
                "Groq" => 1,
                "NVIDIA" => 2,
                "OpenRouter" => 3,
                _ => 50 + p.Priority
            };
        }

        return providers.OrderBy(Rank).ThenBy(p => p.Priority).ToList();
    }

    private string? ResolveApiKey(AIProvider provider)
    {
        var key = _configuration[provider.ApiKeyConfigName];
        if (!string.IsNullOrWhiteSpace(key) && !string.Equals(key, "MOCK_KEY", StringComparison.OrdinalIgnoreCase))
        {
            return key.Trim();
        }

        // Backward-compatible short names
        return provider.Name.Trim().ToLowerInvariant() switch
        {
            "gemini" => _configuration["AIGateway:GeminiApiKey"],
            "groq" => _configuration["AIGateway:GroqApiKey"],
            "nvidia" => _configuration["AIGateway:NvidiaApiKey"],
            "openrouter" => _configuration["AIGateway:OpenRouterApiKey"],
            _ => null
        };
    }

    private async Task<string> CallProviderApiAsync(
        AIProvider provider,
        AIModel model,
        AIGatewayRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        if (string.Equals(provider.Name, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return await CallGeminiGenerateContentAsync(provider, model, request, apiKey, cancellationToken);
        }

        return await CallOpenAiCompatibleAsync(provider, model, request, apiKey, cancellationToken);
    }

    private async Task<string> CallGeminiGenerateContentAsync(
        AIProvider provider,
        AIModel model,
        AIGatewayRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/models/{model.ModelName}:generateContent";

        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = request.SystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = request.UserPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens,
                responseMimeType = "application/json"
            }
        };

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        httpReq.Headers.TryAddWithoutValidation("X-goog-api-key", apiKey);

        using var res = await client.SendAsync(httpReq, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Provider returned HTTP {(int)res.StatusCode}: {Truncate(body, 800)}");
        }

        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var content = candidates[0].GetProperty("content");
            if (content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
            {
                var text = parts[0].GetProperty("text").GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        throw new HttpRequestException("Gemini response did not contain text content.");
    }

    private async Task<string> CallOpenAiCompatibleAsync(
        AIProvider provider,
        AIModel model,
        AIGatewayRequest request,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var endpoint = provider.BaseUrl.TrimEnd('/') + "/chat/completions";

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model.ModelName,
            ["messages"] = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            ["temperature"] = request.Temperature,
            ["max_tokens"] = request.MaxTokens
        };

        // Ask OpenAI-compatible providers for JSON when supported.
        if (!string.Equals(provider.Name, "NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            payload["response_format"] = new { type = "json_object" };
        }

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        httpReq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

        if (string.Equals(provider.Name, "OpenRouter", StringComparison.OrdinalIgnoreCase))
        {
            httpReq.Headers.TryAddWithoutValidation("HTTP-Referer", "https://devpilot-ai.local");
            httpReq.Headers.TryAddWithoutValidation("X-Title", "DevPilot AI");
        }

        using var res = await client.SendAsync(httpReq, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            // Retry once without response_format if provider rejects it.
            if ((int)res.StatusCode == 400 && payload.ContainsKey("response_format"))
            {
                payload.Remove("response_format");
                using var retryReq = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                retryReq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
                if (string.Equals(provider.Name, "OpenRouter", StringComparison.OrdinalIgnoreCase))
                {
                    retryReq.Headers.TryAddWithoutValidation("HTTP-Referer", "https://devpilot-ai.local");
                    retryReq.Headers.TryAddWithoutValidation("X-Title", "DevPilot AI");
                }

                using var retryRes = await client.SendAsync(retryReq, cancellationToken);
                var retryBody = await retryRes.Content.ReadAsStringAsync(cancellationToken);
                if (!retryRes.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Provider returned HTTP {(int)retryRes.StatusCode}: {Truncate(retryBody, 800)}");
                }

                return ExtractOpenAiContent(retryBody);
            }

            throw new HttpRequestException($"Provider returned HTTP {(int)res.StatusCode}: {Truncate(body, 800)}");
        }

        return ExtractOpenAiContent(body);
    }

    private static string ExtractOpenAiContent(string body)
    {
        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var choice = choices[0];
            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                var text = content.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        throw new HttpRequestException("OpenAI-compatible response did not contain message content.");
    }

    private static int EstimateTokens(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, text.Length / 4);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
