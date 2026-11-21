using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Payments.DTOs;

public class CreatePaymobCheckoutRequestDto
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string PlanCode { get; set; } = "PRO";
}

public record PaymobCheckoutDto(
    string MerchantOrderId,
    string CheckoutUrl,
    int AmountCents,
    string Currency,
    decimal AmountUsd,
    decimal AmountEgp,
    decimal ExchangeRateUsdToEgp
);

public record PaymentOrderStatusDto(
    string MerchantOrderId,
    string Status,
    string PlanCode,
    DateTime? PaidAt
);
