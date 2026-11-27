using System.Diagnostics;
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
    private readonly IAuditService? _auditService;

    public AIGatewayService(
        DevPilotDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AIGatewayService> logger,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<AIGatewayResponse> ExecuteAsync(AIGatewayRequest request, CancellationToken cancellationToken = default)
    {
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId.Trim();

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
                ErrorMessage: "No AI providers are configured. Seed providers and add API keys.",
                CorrelationId: correlationId
            );
        }

        var errors = new List<string>();
        var retrySequence = 0;

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
                        ?? provider.Models.FirstOrDefault();
            if (model == null || model.AIModelId <= 0)
            {
                errors.Add($"{provider.Name}: no active model configured");
                continue;
            }

            var startedAt = DateTime.UtcNow;
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
                var totalTokens = promptTokens + completionTokens;

                var (aiRequestId, usageEventId) = await PersistAiTelemetryAsync(
                    request,
                    provider,
                    model,
                    success: true,
                    promptTokens,
                    completionTokens,
                    sw.ElapsedMilliseconds,
                    errorMessage: null,
                    correlationId,
                    retrySequence,
                    startedAt,
                    cancellationToken);

                await TryAuditAiAsync(
                    SystemAuditEventTypes.AiRequestCompleted,
                    request,
                    provider.Name,
                    model.ModelName,
                    success: true,
                    promptTokens,
                    completionTokens,
                    sw.ElapsedMilliseconds,
                    aiRequestId,
                    usageEventId,
                    correlationId,
                    error: null,
                    cancellationToken);

                return new AIGatewayResponse(
                    IsSuccess: true,
                    Content: responseContent,
                    ProviderName: provider.Name,
                    ModelName: model.ModelName,
                    PromptTokens: promptTokens,
                    CompletionTokens: completionTokens,
                    DurationMs: sw.ElapsedMilliseconds,
                    AIRequestId: aiRequestId,
                    AIUsageEventId: usageEventId,
                    TotalTokens: totalTokens,
                    CorrelationId: correlationId
                );
            }
            catch (Exception ex)
            {
                sw.Stop();
                retrySequence++;
                _logger.LogError(ex, "AI Provider {ProviderName} failed for task {TaskType}", provider.Name, request.TaskType);

                var isRateLimited = ex.Message.Contains("429", StringComparison.Ordinal)
                                    || ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase)
                                    || ex.Message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase);

                provider.Status = isRateLimited ? ProviderStatus.RateLimited : ProviderStatus.Active;
                provider.RateLimitedUntil = isRateLimited ? DateTime.UtcNow.AddMinutes(5) : null;
                provider.LastError = Truncate(ex.Message, 1000);
                errors.Add($"{provider.Name}: {Truncate(ex.Message, 300)}");

                try
                {
                    var promptTokens = EstimateTokens(request.SystemPrompt) + EstimateTokens(request.UserPrompt);
                    var (aiRequestId, usageEventId) = await PersistAiTelemetryAsync(
                        request,
                        provider,
                        model,
                        success: false,
                        promptTokens,
                        completionTokens: 0,
                        sw.ElapsedMilliseconds,
                        errorMessage: Truncate(ex.Message, 1000),
                        correlationId,
                        retrySequence - 1,
                        startedAt,
                        cancellationToken);

                    await TryAuditAiAsync(
                        SystemAuditEventTypes.AiRequestFailed,
                        request,
                        provider.Name,
                        model.ModelName,
                        success: false,
                        promptTokens,
                        0,
                        sw.ElapsedMilliseconds,
                        aiRequestId,
                        usageEventId,
                        correlationId,
                        Truncate(ex.Message, 300),
                        cancellationToken);

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception telemetryEx)
                {
                    // Don't 500 the request if telemetry save fails.
                    _logger.LogWarning(telemetryEx, "Failed to persist failure telemetry for {ProviderName}", provider.Name);
                    try
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogWarning(saveEx, "Failed to persist provider status after {ProviderName} failure", provider.Name);
                    }
                }
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
            ErrorMessage: $"All AI Providers failed: {string.Join(" | ", errors)}",
            CorrelationId: correlationId
        );
    }

    private async Task<(int? AIRequestId, long? AIUsageEventId)> PersistAiTelemetryAsync(
        AIGatewayRequest request,
        AIProvider provider,
        AIModel model,
        bool success,
        int promptTokens,
        int completionTokens,
        long durationMs,
        string? errorMessage,
        string correlationId,
        int retrySequence,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var aiReq = new AIRequest
        {
            UserId = request.UserId,
            AIProviderId = provider.AIProviderId,
            AIModelId = model.AIModelId,
            DebuggingAttemptId = request.DebuggingAttemptId,
            TaskType = request.TaskType,
            PromptTokensUsed = promptTokens,
            CompletionTokensUsed = completionTokens,
            TotalTokensUsed = promptTokens + completionTokens,
            DurationMs = durationMs,
            Success = success,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AIRequests.Add(aiReq);
        await _dbContext.SaveChangesAsync(cancellationToken);

        long? usageEventId = null;
        if (request.UserId is int userId && userId > 0)
        {
            var usageEvent = new AIUsageEvent
            {
                AIRequestId = aiReq.AIRequestId,
                UserId = userId,
                ProblemId = request.ProblemId,
                ProviderId = provider.AIProviderId,
                ModelId = model.AIModelId,
                OperationType = request.TaskType,
                InputTokens = promptTokens,
                OutputTokens = completionTokens,
                TotalTokens = promptTokens + completionTokens,
                CostType = "Credits",
                Status = success ? "Success" : "Failed",
                CorrelationId = correlationId,
                IdempotencyKey = Truncate(request.Purpose, 150),
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                DurationMs = durationMs,
                RetrySequence = retrySequence,
                ErrorCode = success ? null : Truncate(errorMessage, 100),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AIUsageEvents.Add(usageEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);
            usageEventId = usageEvent.AIUsageEventId;
        }

        return (aiReq.AIRequestId, usageEventId);
    }

    private async Task TryAuditAiAsync(
        string action,
        AIGatewayRequest request,
        string providerName,
        string modelName,
        bool success,
        int promptTokens,
        int completionTokens,
        long durationMs,
        int? aiRequestId,
        long? usageEventId,
        string correlationId,
        string? error,
        CancellationToken cancellationToken)
    {
        if (_auditService == null) return;

        try
        {
            await _auditService.LogAsync(
                action,
                nameof(AIRequest),
                (aiRequestId ?? 0).ToString(),
                oldValues: null,
                newValues: new
                {
                    request.TaskType,
                    Purpose = request.Purpose,
                    request.UserId,
                    request.ProblemId,
                    Provider = providerName,
                    Model = modelName,
                    Success = success,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    DurationMs = durationMs,
                    AIRequestId = aiRequestId,
                    AIUsageEventId = usageEventId,
                    CorrelationId = correlationId,
                    Error = error
                },
                userId: request.UserId,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write AI audit event for {TaskType}", request.TaskType);
        }
    }

    private static List<AIProvider> OrderProvidersForTask(List<AIProvider> providers, string taskType)
    {
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

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
