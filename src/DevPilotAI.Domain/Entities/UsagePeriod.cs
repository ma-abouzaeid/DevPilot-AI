namespace DevPilotAI.Domain.Entities;

public class UsagePeriod
{
    public long UsagePeriodId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public int CreditsAllocated { get; set; }
    public int CreditsReserved { get; set; }
    public int CreditsUsed { get; set; }
    public int CreditsRefunded { get; set; }

    public long RequestCount { get; set; }
    public long TokenCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UsageLedger> UsageLedgers { get; set; } = new List<UsageLedger>();
}
