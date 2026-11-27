using DevPilotAI.Application.Payments.DTOs;
using System.Text.Json;

namespace DevPilotAI.Application.Payments;

public interface IPaymentService
{
    Task<PaymobCheckoutDto> CreatePaymobCheckoutAsync(int userId, string planCode, CancellationToken cancellationToken = default);
    Task HandlePaymobWebhookAsync(JsonElement payload, string? hmacQuery, CancellationToken cancellationToken = default);
    Task<(bool Success, string RedirectUrl)> HandlePaymobReturnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken = default);
    Task<PaymentOrderStatusDto?> GetOrderStatusAsync(int userId, string merchantOrderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentOrderListItemDto>> ListMyOrdersAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentOrderListItemDto>> ListAllOrdersAsync(CancellationToken cancellationToken = default);
}