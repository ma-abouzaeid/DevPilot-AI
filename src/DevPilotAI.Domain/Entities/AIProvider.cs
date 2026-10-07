using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class AIProvider
{
    public int AIProviderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKeyConfigName { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public ProviderStatus Status { get; set; } = ProviderStatus.Active;
    public DateTime? RateLimitedUntil { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AIModel> Models { get; set; } = new List<AIModel>();
    public ICollection<AIRequest> Requests { get; set; } = new List<AIRequest>();
}
