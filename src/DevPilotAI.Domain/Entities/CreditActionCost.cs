namespace DevPilotAI.Domain.Entities;

/// <summary>Admin-configurable credit deduction for a billable AI action.</summary>
public class CreditActionCost
{
    public string ActionKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool ShowOnPlansPage { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
