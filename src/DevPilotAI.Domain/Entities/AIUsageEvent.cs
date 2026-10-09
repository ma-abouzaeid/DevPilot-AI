namespace DevPilotAI.Domain.Entities;

public class AIUsageEvent
{
    public long AIUsageEventId { get; set; }
    public int AIRequestId { get; set; }
    public AIRequest AIRequest { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? ProblemId { get; set; }
    public Problem? Problem { get; set; }

    public int ProviderId { get; set; }
    public AIProvider Provider { get; set; } = null!;

    public int? ModelId { get; set; }
    public AIModel? Model { get; set; }

    public string OperationType { get; set; } = string.Empty;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string CostType { get; set; } = "Free";
    public int? CreditsReserved { get; set; }
    public int? CreditsCharged { get; set; }
    public int? CreditsRefunded { get; set; }
    public string Status { get; set; } = "Success";
    public string? ProviderRequestId { get; set; }
    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public int RetrySequence { get; set; } = 0;
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UsageLedger> UsageLedgers { get; set; } = new List<UsageLedger>();
}
