namespace DevPilotAI.Domain.Entities;

public class UsageLedger
{
    public long UsageLedgerId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public long UsagePeriodId { get; set; }
    public UsagePeriod UsagePeriod { get; set; } = null!;

    public long? AIUsageEventId { get; set; }
    public AIUsageEvent? AIUsageEvent { get; set; }

    public string EntryType { get; set; } = "CHARGE";
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
