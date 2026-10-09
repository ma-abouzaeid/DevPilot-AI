namespace DevPilotAI.Domain.Entities;

public class Plan
{
    public int PlanId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsDefault { get; set; }
    public bool IsPublic { get; set; } = true;
    public string PeriodType { get; set; } = "Monthly";
    public int CreditsPerPeriod { get; set; } = 100;
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    public ICollection<UsagePeriod> UsagePeriods { get; set; } = new List<UsagePeriod>();
}
