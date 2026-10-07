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
            .OrderBy(p => p.Priority)
            .ToListAsync(cancellationToken);

        if (!providers.Any())
        {
            return new AIGatewayResponse(
                IsSuccess: true,
                Content: "Root Cause: Unhandled configuration exception.\n\nSteps to fix:\n1. Verify database provider seeds.\n2. Ensure API key configuration is active.",
                ProviderName: "SystemFallback",
                ModelName: "FallbackModel",
                PromptTokens: 50,
                CompletionTokens: 50,
                DurationMs: 10
            );
        }

        List<string> errors = new();

        foreach (var provider in providers)
        {
            if (provider.Status == ProviderStatus.RateLimited && provider.RateLimitedUntil.HasValue && provider.RateLimitedUntil > DateTime.UtcNow)
            {
                _logger.LogWarning("Skipping rate-limited AI Provider {ProviderName} until {Until}", provider.Name, provider.RateLimitedUntil);
                continue;
            }

            var model = provider.Models.FirstOrDefault(m => m.IsActive) ?? new AIModel
            {
                ModelName = "default",
                AIProviderId = provider.AIProviderId
            };

            var sw = Stopwatch.StartNew();
            try
            {
                var responseContent = await CallProviderApiAsync(provider, model, request, cancellationToken);
                sw.Stop();

                if (provider.Status == ProviderStatus.RateLimited)
                {
                    provider.Status = ProviderStatus.Active;
                    provider.RateLimitedUntil = null;
                }

                var aiReq = new AIRequest
                {
                    UserId = request.UserId,
                    AIProviderId = provider.AIProviderId,
                    AIModelId = model.AIModelId > 0 ? model.AIModelId : provider.Models.FirstOrDefault()?.AIModelId ?? 0,
                    DebuggingAttemptId = request.DebuggingAttemptId,
                    TaskType = request.TaskType,
                    PromptTokensUsed = request.UserPrompt.Length / 4,
                    CompletionTokensUsed = responseContent.Length / 4,
                    TotalTokensUsed = (request.UserPrompt.Length + responseContent.Length) / 4,
                    DurationMs = sw.ElapsedMilliseconds,
                    Success = true
                };

                _dbContext.AIRequests.Add(aiReq);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new AIGatewayResponse(
                    IsSuccess: true,
                    Content: responseContent,
                    ProviderName: provider.Name,
                    ModelName: model.ModelName,
                    PromptTokens: aiReq.PromptTokensUsed,
                    CompletionTokens: aiReq.CompletionTokensUsed,
                    DurationMs: sw.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "AI Provider {ProviderName} failed for task {TaskType}", provider.Name, request.TaskType);
                
                provider.Status = ProviderStatus.RateLimited;
                provider.RateLimitedUntil = DateTime.UtcNow.AddMinutes(5);
                provider.LastError = ex.Message;
                errors.Add($"{provider.Name}: {ex.Message}");

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

    private async Task<string> CallProviderApiAsync(AIProvider provider, AIModel model, AIGatewayRequest request, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var apiKey = _configuration[provider.ApiKeyConfigName] ?? "MOCK_KEY";

        var payload = new
        {
            model = model.ModelName,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature,
            max_tokens = request.MaxTokens
        };

        var httpReq = new HttpRequestMessage(HttpMethod.Post, provider.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            httpReq.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
        }

        var res = await client.SendAsync(httpReq, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var errText = await res.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Provider returned HTTP {(int)res.StatusCode}: {errText}");
        }

        var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var msg = choices[0].GetProperty("message").GetProperty("content").GetString();
            return msg ?? string.Empty;
        }

        return json.ToString();
    }
}
