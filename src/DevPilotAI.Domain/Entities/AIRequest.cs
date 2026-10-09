namespace DevPilotAI.Domain.Entities;

public class AIRequest
{
    public int AIRequestId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }

    public int AIProviderId { get; set; }
    public AIProvider Provider { get; set; } = null!;

    public int AIModelId { get; set; }
    public AIModel Model { get; set; } = null!;

    public int? DebuggingAttemptId { get; set; }
    public DebuggingAttempt? DebuggingAttempt { get; set; }

    public string TaskType { get; set; } = string.Empty;
    public int PromptTokensUsed { get; set; }
    public int CompletionTokensUsed { get; set; }
    public int TotalTokensUsed { get; set; }
    public long DurationMs { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AIUsageEvent> AIUsageEvents { get; set; } = new List<AIUsageEvent>();
}
