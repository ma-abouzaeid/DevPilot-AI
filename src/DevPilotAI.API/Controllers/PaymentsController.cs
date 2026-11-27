using System.Security.Claims;
using System.Text.Json;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Payments;
using DevPilotAI.Application.Payments.DTOs;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.General)]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments)
    {
        _payments = payments;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private int GetRequiredUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var id) || id <= 0)
        {
            throw AppError.Unauthorized("User identity claim is missing or invalid.");
        }

        return id;
    }

    /// <summary>Creates a Paymob Unified Checkout session for a paid plan (PRO).</summary>
    [HttpPost("paymob/checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreatePaymobCheckoutRequestDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _payments.CreatePaymobCheckoutAsync(
            GetRequiredUserId(),
            dto.PlanCode,
            cancellationToken);
        return Ok(ApiResponse<PaymobCheckoutDto>.SuccessResult(result, "Checkout created.", GetTraceId()));
    }

    [HttpGet("paymob/orders/{merchantOrderId}")]
    [Authorize]
    public async Task<IActionResult> GetOrder(string merchantOrderId, CancellationToken cancellationToken)
    {
        var result = await _payments.GetOrderStatusAsync(GetRequiredUserId(), merchantOrderId, cancellationToken);
        if (result == null) throw AppError.NotFound("Payment order was not found.");
        return Ok(ApiResponse<PaymentOrderStatusDto>.SuccessResult(result, "Payment order loaded.", GetTraceId()));
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var items = await _payments.ListMyOrdersAsync(GetRequiredUserId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PaymentOrderListItemDto>>.SuccessResult(items, "Payments loaded.", GetTraceId()));
    }

    [HttpGet]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> ListAll(CancellationToken cancellationToken)
    {
        var items = await _payments.ListAllOrdersAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PaymentOrderListItemDto>>.SuccessResult(items, "Payments loaded.", GetTraceId()));
    }

    /// <summary>Paymob server-to-server transaction callback.</summary>
    [HttpPost("paymob/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        var hmac = Request.Query["hmac"].FirstOrDefault();
        await _payments.HandlePaymobWebhookAsync(doc.RootElement.Clone(), hmac, cancellationToken);
        return Ok(new { success = true });
    }

    /// <summary>Browser return URL after Paymob checkout — verifies HMAC then redirects to plans page.</summary>
    [HttpGet("paymob/return")]
    [AllowAnonymous]
    public async Task<IActionResult> Return(CancellationToken cancellationToken)
    {
        var query = Request.Query.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        var (_, redirectUrl) = await _payments.HandlePaymobReturnAsync(query, cancellationToken);
        return Redirect(redirectUrl);
    }
}
