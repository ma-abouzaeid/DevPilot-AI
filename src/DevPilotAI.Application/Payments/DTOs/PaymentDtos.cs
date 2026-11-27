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

public record PaymentOrderListItemDto(
    long PaymentOrderId,
    string MerchantOrderId,
    string Status,
    string PlanCode,
    string PlanName,
    decimal AmountUsd,
    decimal AmountEgp,
    string Currency,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? FailureReason,
    int UserId = 0,
    string? UserEmail = null
);
