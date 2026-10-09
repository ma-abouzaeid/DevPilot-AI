namespace DevPilotAI.Domain.Entities;

public class UserSubscription
{
    public long SubscriptionId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public string Status { get; set; } = "Active";
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public int? AssignedByUserId { get; set; }
    public User? AssignedByUser { get; set; }
    public string AssignmentSource { get; set; } = "System";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
