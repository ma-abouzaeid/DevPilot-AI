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
    public bool SaleEnabled { get; set; }
    // 0-100 when SaleEnabled
    public decimal SaleDiscountPercent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public decimal GetEffectivePriceUsd()
    {
        var price = Price ?? 0m;
        if (!SaleEnabled || SaleDiscountPercent <= 0m || price <= 0m)
        {
            return price;
        }

        var pct = SaleDiscountPercent;
        if (pct > 100m) pct = 100m;
        return Math.Round(price * (1m - pct / 100m), 2, MidpointRounding.AwayFromZero);
    }

    // Paid: monthly end date. Free: null.
    public DateTime? ComputeSubscriptionEndDate(DateTime startUtc)
    {
        if ((Price ?? 0m) <= 0m)
        {
            return null;
        }

        return string.Equals(PeriodType, "Yearly", StringComparison.OrdinalIgnoreCase)
            ? startUtc.AddYears(1)
            : startUtc.AddMonths(1);
    }

    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    public ICollection<UsagePeriod> UsagePeriods { get; set; } = new List<UsagePeriod>();
}
