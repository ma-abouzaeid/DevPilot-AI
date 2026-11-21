namespace DevPilotAI.Domain.Entities;

public class PaymentOrder
{
    public long PaymentOrderId { get; set; }
    public string MerchantOrderId { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public string Provider { get; set; } = "Paymob";
    public string Status { get; set; } = "Pending";

    public decimal AmountUsd { get; set; }
    public decimal AmountEgp { get; set; }
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "EGP";
    public decimal ExchangeRateUsdToEgp { get; set; } = 50m;

    public string? PaymobIntentionId { get; set; }
    public string? PaymobOrderId { get; set; }
    public string? PaymobTransactionId { get; set; }
    public string? CheckoutUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public string? FailureReason { get; set; }
}
