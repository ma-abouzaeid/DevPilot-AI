using System.Globalization;
using System.Text.Json;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Payments.DTOs;
using DevPilotAI.Application.Plans;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Application.Payments;

public class PaymentService : IPaymentService
{
    private readonly IApplicationDbContext _db;
    private readonly IPaymobGateway _paymob;
    private readonly IPlanUsageService _plans;
    private readonly IConfiguration _configuration;

    public PaymentService(
        IApplicationDbContext db,
        IPaymobGateway paymob,
        IPlanUsageService plans,
        IConfiguration configuration)
    {
        _db = db;
        _paymob = paymob;
        _plans = plans;
        _configuration = configuration;
    }

    public async Task<PaymobCheckoutDto> CreatePaymobCheckoutAsync(
        int userId,
        string planCode,
        CancellationToken cancellationToken = default)
    {
        if (!_paymob.IsConfigured)
        {
            throw AppError.InvalidRequest(
                "Paymob is not configured on the server yet.",
                "PAYMOB_NOT_CONFIGURED");
        }

        var code = (planCode ?? string.Empty).Trim().ToUpperInvariant();
        var plan = await _db.Plans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == code && p.Status == "Active" && p.IsPublic, cancellationToken)
            ?? throw AppError.NotFound($"Plan '{code}' was not found.");

        if ((plan.Price ?? 0m) <= 0m)
        {
            throw AppError.InvalidRequest("This plan does not require payment.", "PLAN_FREE");
        }

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.Unauthorized("User was not found.");

        var rate = GetUsdToEgpRate();
        var amountUsd = plan.GetEffectivePriceUsd();
        if (amountUsd <= 0m)
        {
            throw AppError.InvalidRequest(
                "Sale price is zero. Activate this plan without payment or adjust the sale discount.",
                "PLAN_FREE");
        }

        var amountEgp = Math.Round(amountUsd * rate, 2, MidpointRounding.AwayFromZero);
        var amountCents = (int)Math.Round(amountEgp * 100m, MidpointRounding.AwayFromZero);
        if (amountCents < 100)
        {
            throw AppError.InvalidRequest("Calculated charge amount is invalid.", "INVALID_AMOUNT");
        }

        var merchantOrderId = $"DP-{userId}-{plan.PlanId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var order = new PaymentOrder
        {
            MerchantOrderId = merchantOrderId,
            UserId = userId,
            PlanId = plan.PlanId,
            Provider = "Paymob",
            Status = "Pending",
            AmountUsd = amountUsd,
            AmountEgp = amountEgp,
            AmountCents = amountCents,
            Currency = "EGP",
            ExchangeRateUsdToEgp = rate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PaymentOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        var webhookBase = (_configuration["Paymob:WebhookBaseUrl"] ?? "http://devpilot-ai.runasp.net").TrimEnd('/');
        var notificationUrl = $"{webhookBase}/api/Payments/paymob/webhook";
        var redirectionUrl = $"{webhookBase}/api/Payments/paymob/return";

        var nameParts = (user.Name ?? "DevPilot User").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = nameParts.Length > 0 ? nameParts[0] : "DevPilot";
        var last = nameParts.Length > 1 ? nameParts[1] : "User";
        var integrationId = int.TryParse(_configuration["Paymob:IntegrationId"], out var iid) ? iid : 0;

        var intention = await _paymob.CreateIntentionAsync(
            new PaymobIntentionRequest(
                amountCents,
                "EGP",
                new[] { integrationId },
                merchantOrderId,
                notificationUrl,
                redirectionUrl,
                new PaymobBillingData(first, last, user.Email, "+201000000000"),
                $"{plan.Name} plan"),
            cancellationToken);

        order.PaymobIntentionId = intention.IntentionId;
        order.PaymobOrderId = intention.OrderId;
        order.CheckoutUrl = intention.CheckoutUrl;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new PaymobCheckoutDto(
            merchantOrderId,
            intention.CheckoutUrl,
            amountCents,
            "EGP",
            amountUsd,
            amountEgp,
            rate);
    }

    public async Task HandlePaymobWebhookAsync(
        JsonElement payload,
        string? hmacQuery,
        CancellationToken cancellationToken = default)
    {
        var obj = payload.TryGetProperty("obj", out var o) ? o : payload;
        var flat = FlattenTransaction(obj);
        var hmac = hmacQuery
            ?? (payload.TryGetProperty("hmac", out var h) ? h.GetString() : null)
            ?? string.Empty;

        if (!_paymob.VerifyTransactionHmac(flat, hmac))
        {
            throw AppError.Forbidden("Invalid Paymob HMAC.");
        }

        await ApplyTransactionAsync(flat, cancellationToken);
    }

    public async Task<(bool Success, string RedirectUrl)> HandlePaymobReturnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken = default)
    {
        var frontend = (_configuration["Paymob:FrontendReturnUrl"]
            ?? _configuration["Frontend:BaseUrl"]
            ?? "https://devpilot-ai-three.vercel.app/plans.html").Trim();
        if (!frontend.Contains("plans.html", StringComparison.OrdinalIgnoreCase))
        {
            frontend = frontend.TrimEnd('/') + "/plans.html";
        }

        var hmac = query.TryGetValue("hmac", out var h) ? h : string.Empty;
        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in query)
        {
            if (!string.Equals(kv.Key, "hmac", StringComparison.OrdinalIgnoreCase))
            {
                flat[kv.Key] = kv.Value ?? string.Empty;
            }
        }

        // Normalize GET callback keys to HMAC field names.
        if (flat.TryGetValue("id", out var id)) flat["id"] = id;
        if (flat.TryGetValue("order", out var order) && !flat.ContainsKey("order_id")) flat["order_id"] = order;

        var successFlag = flat.TryGetValue("success", out var s) &&
                          (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "True");

        if (!string.IsNullOrWhiteSpace(hmac) && _paymob.VerifyTransactionHmac(flat, hmac))
        {
            await ApplyTransactionAsync(flat, cancellationToken);
        }

        var sep = frontend.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return (successFlag, $"{frontend}{sep}payment={(successFlag ? "success" : "failed")}");
    }

    public async Task<PaymentOrderStatusDto?> GetOrderStatusAsync(
        int userId,
        string merchantOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _db.PaymentOrders.AsNoTracking()
            .Include(o => o.Plan)
            .FirstOrDefaultAsync(
                o => o.UserId == userId && o.MerchantOrderId == merchantOrderId,
                cancellationToken);
        if (order == null) return null;
        return new PaymentOrderStatusDto(order.MerchantOrderId, order.Status, order.Plan.Code, order.PaidAt);
    }

    public async Task<IReadOnlyList<PaymentOrderListItemDto>> ListMyOrdersAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PaymentOrders.AsNoTracking()
            .Include(o => o.Plan)
            .Include(o => o.User)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(50)
            .Select(o => new PaymentOrderListItemDto(
                o.PaymentOrderId,
                o.MerchantOrderId,
                o.Status,
                o.Plan.Code,
                o.Plan.Name,
                o.AmountUsd,
                o.AmountEgp,
                o.Currency,
                o.CreatedAt,
                o.PaidAt,
                o.FailureReason,
                o.UserId,
                o.User.Email))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentOrderListItemDto>> ListAllOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.PaymentOrders.AsNoTracking()
            .Include(o => o.Plan)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .Select(o => new PaymentOrderListItemDto(
                o.PaymentOrderId,
                o.MerchantOrderId,
                o.Status,
                o.Plan.Code,
                o.Plan.Name,
                o.AmountUsd,
                o.AmountEgp,
                o.Currency,
                o.CreatedAt,
                o.PaidAt,
                o.FailureReason,
                o.UserId,
                o.User.Email))
            .ToListAsync(cancellationToken);
    }

    private async Task ApplyTransactionAsync(
        IReadOnlyDictionary<string, string> flat,
        CancellationToken cancellationToken)
    {
        var success = flat.TryGetValue("success", out var s) &&
                      (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "True");
        var pending = flat.TryGetValue("pending", out var p) &&
                      (string.Equals(p, "true", StringComparison.OrdinalIgnoreCase) || p == "True");

        var merchantOrderId =
            flat.TryGetValue("merchant_order_id", out var m) ? m :
            flat.TryGetValue("special_reference", out var sr) ? sr :
            null;

        // Sometimes only order id; match PaymobOrderId.
        PaymentOrder? order = null;
        if (!string.IsNullOrWhiteSpace(merchantOrderId))
        {
            order = await _db.PaymentOrders
                .FirstOrDefaultAsync(o => o.MerchantOrderId == merchantOrderId, cancellationToken);
        }

        if (order == null && flat.TryGetValue("order_id", out var oid) && !string.IsNullOrWhiteSpace(oid))
        {
            order = await _db.PaymentOrders
                .FirstOrDefaultAsync(o => o.PaymobOrderId == oid, cancellationToken);
        }

        if (order == null) return;
        if (string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase)) return;

        if (flat.TryGetValue("id", out var txId))
        {
            order.PaymobTransactionId = txId;
        }

        if (pending)
        {
            order.Status = "Pending";
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!success)
        {
            order.Status = "Failed";
            order.FailureReason = flat.TryGetValue("data.message", out var msg) ? msg : "Payment failed";
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        order.Status = "Paid";
        order.PaidAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var planCode = await _db.Plans.AsNoTracking()
            .Where(pl => pl.PlanId == order.PlanId)
            .Select(pl => pl.Code)
            .FirstOrDefaultAsync(cancellationToken) ?? "PRO";

        await _plans.ActivatePlanAsync(order.UserId, planCode, cancellationToken, assignmentSource: "Paymob");
    }

    private decimal GetUsdToEgpRate()
    {
        var raw = _configuration["Paymob:UsdToEgpRate"];
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) && rate > 0)
        {
            return rate;
        }

        return 50m;
    }

    private static Dictionary<string, string> FlattenTransaction(JsonElement obj)
    {
        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Put(string key, JsonElement el)
        {
            flat[key] = el.ValueKind switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.String => el.GetString() ?? string.Empty,
                _ => el.ToString()
            };
        }

        if (obj.TryGetProperty("amount_cents", out var ac)) Put("amount_cents", ac);
        if (obj.TryGetProperty("created_at", out var ca)) Put("created_at", ca);
        if (obj.TryGetProperty("currency", out var cur)) Put("currency", cur);
        if (obj.TryGetProperty("error_occured", out var eo)) Put("error_occured", eo);
        if (obj.TryGetProperty("has_parent_transaction", out var hpt)) Put("has_parent_transaction", hpt);
        if (obj.TryGetProperty("id", out var id)) Put("id", id);
        if (obj.TryGetProperty("integration_id", out var iid)) Put("integration_id", iid);
        if (obj.TryGetProperty("is_3d_secure", out var i3)) Put("is_3d_secure", i3);
        if (obj.TryGetProperty("is_auth", out var ia)) Put("is_auth", ia);
        if (obj.TryGetProperty("is_capture", out var ic)) Put("is_capture", ic);
        if (obj.TryGetProperty("is_refunded", out var ir)) Put("is_refunded", ir);
        if (obj.TryGetProperty("is_standalone_payment", out var isp)) Put("is_standalone_payment", isp);
        if (obj.TryGetProperty("is_voided", out var iv)) Put("is_voided", iv);
        if (obj.TryGetProperty("owner", out var ow)) Put("owner", ow);
        if (obj.TryGetProperty("pending", out var pe)) Put("pending", pe);
        if (obj.TryGetProperty("success", out var su)) Put("success", su);

        if (obj.TryGetProperty("order", out var orderEl))
        {
            if (orderEl.ValueKind == JsonValueKind.Object && orderEl.TryGetProperty("id", out var oid))
            {
                Put("order_id", oid);
            }
            else if (orderEl.ValueKind == JsonValueKind.Number || orderEl.ValueKind == JsonValueKind.String)
            {
                Put("order_id", orderEl);
            }

            if (orderEl.ValueKind == JsonValueKind.Object &&
                orderEl.TryGetProperty("merchant_order_id", out var moi))
            {
                Put("merchant_order_id", moi);
            }
        }

        if (obj.TryGetProperty("merchant_order_id", out var moi2)) Put("merchant_order_id", moi2);

        if (obj.TryGetProperty("source_data", out var sd) && sd.ValueKind == JsonValueKind.Object)
        {
            if (sd.TryGetProperty("pan", out var pan)) Put("source_data.pan", pan);
            if (sd.TryGetProperty("sub_type", out var st)) Put("source_data.sub_type", st);
            if (sd.TryGetProperty("type", out var ty)) Put("source_data.type", ty);
        }

        return flat;
    }
}