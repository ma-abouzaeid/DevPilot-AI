using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.AIGateway;

public static class AiProviderSeeder
{
    private static readonly (string Name, string BaseUrl, string ApiKeyConfigName, int Priority, string Model, int Context)[] Providers =
    [
        ("Gemini", "https://generativelanguage.googleapis.com/v1beta", "AIGateway:GeminiApiKey", 1, "gemini-flash-latest", 1_000_000),
        ("Groq", "https://api.groq.com/openai/v1", "AIGateway:GroqApiKey", 2, "openai/gpt-oss-120b", 131_072),
        ("NVIDIA", "https://integrate.api.nvidia.com/v1", "AIGateway:NvidiaApiKey", 3, "nvidia/nemotron-3.5-lightning-30b-a3b", 262_144),
        ("OpenRouter", "https://openrouter.ai/api/v1", "AIGateway:OpenRouterApiKey", 4, "qwen/qwen3-coder:free", 262_144)
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevPilotDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AiProviderSeeder");

        try
        {
            await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping AI provider seed because database is unavailable.");
            return;
        }

        foreach (var item in Providers)
        {
            var existing = await db.AIProviders
                .Include(p => p.Models)
                .FirstOrDefaultAsync(p => p.Name == item.Name, cancellationToken);

            if (existing == null)
            {
                var provider = new AIProvider
                {
                    Name = item.Name,
                    BaseUrl = item.BaseUrl,
                    ApiKeyConfigName = item.ApiKeyConfigName,
                    Priority = item.Priority,
                    Status = ProviderStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    Models =
                    [
                        new AIModel
                        {
                            ModelName = item.Model,
                            IsActive = true,
                            MaxContextTokens = item.Context,
                            CreatedAt = DateTime.UtcNow
                        }
                    ]
                };

                db.AIProviders.Add(provider);
                logger.LogInformation("Seeded AI provider {Provider} with model {Model}", item.Name, item.Model);
                continue;
            }

            existing.BaseUrl = item.BaseUrl;
            existing.ApiKeyConfigName = item.ApiKeyConfigName;
            existing.Priority = item.Priority;

            if (existing.Status == ProviderStatus.Disabled)
            {
                existing.Status = ProviderStatus.Active;
            }

            var activeModel = existing.Models.FirstOrDefault(m => m.IsActive);
            if (activeModel == null)
            {
                existing.Models.Add(new AIModel
                {
                    ModelName = item.Model,
                    IsActive = true,
                    MaxContextTokens = item.Context,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else if (!string.Equals(activeModel.ModelName, item.Model, StringComparison.Ordinal))
            {
                activeModel.ModelName = item.Model;
                activeModel.MaxContextTokens = item.Context;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
