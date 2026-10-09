namespace DevPilotAI.Domain.Entities;

public class PlanFeature
{
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public int FeatureId { get; set; }
    public Feature Feature { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;
    public int? LimitValue { get; set; }
    public string? ConfigJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
