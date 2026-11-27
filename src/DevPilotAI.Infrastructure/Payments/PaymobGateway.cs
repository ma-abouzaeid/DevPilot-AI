using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevPilotAI.Infrastructure.Payments;

public class PaymobGateway : IPaymobGateway
{
    private readonly HttpClient _http;
    private readonly PaymobOptions _options;
    private readonly ILogger<PaymobGateway> _logger;

    public PaymobGateway(HttpClient http, IOptions<PaymobOptions> options, ILogger<PaymobGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SecretKey) &&
        !string.IsNullOrWhiteSpace(_options.PublicKey) &&
        !string.IsNullOrWhiteSpace(_options.HmacSecret) &&
        _options.IntegrationId > 0;

    public async Task<PaymobIntentionResult> CreateIntentionAsync(
        PaymobIntentionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw AppError.InvalidRequest(
                "Paymob is not configured. Add SecretKey, PublicKey, HmacSecret, and IntegrationId.",
                "PAYMOB_NOT_CONFIGURED");
        }

        var body = new
        {
            amount = request.AmountCents,
            currency = request.Currency,
            payment_methods = request.PaymentMethods,
            special_reference = request.SpecialReference,
            notification_url = request.NotificationUrl,
            redirection_url = request.RedirectionUrl,
            billing_data = new
            {
                first_name = request.BillingData.FirstName,
                last_name = request.BillingData.LastName,
                email = request.BillingData.Email,
                phone_number = request.BillingData.PhoneNumber,
                street = "NA",
                building = "NA",
                floor = "NA",
                apartment = "NA",
                city = "Cairo",
                country = "EGY",
                state = "Cairo"
            },
            items = new[]
            {
                new
                {
                    name = request.ItemName,
                    amount = request.AmountCents,
                    description = request.ItemName,
                    quantity = 1
                }
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, Combine(_options.BaseUrl, "/v1/intention/"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Token", _options.SecretKey.Trim());
        message.Content = JsonContent.Create(body);

        using var response = await _http.SendAsync(message, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Paymob intention failed: {Status} {Body}", (int)response.StatusCode, raw);
            throw AppError.InvalidRequest(
                "Paymob checkout could not be created. Check integration credentials.",
                "PAYMOB_INTENTION_FAILED");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var clientSecret = GetString(root, "client_secret")
            ?? throw AppError.InvalidRequest("Paymob response missing client_secret.", "PAYMOB_BAD_RESPONSE");
        var intentionId = GetString(root, "id") ?? GetString(root, "intention_id") ?? string.Empty;
        var orderId = GetString(root, "intention_order_id")
            ?? GetNestedString(root, "intention_detail", "id")
            ?? GetString(root, "order");

        var checkoutBase = _options.CheckoutBaseUrl.TrimEnd('/');
        var checkoutUrl =
            $"{checkoutBase}/?publicKey={Uri.EscapeDataString(_options.PublicKey.Trim())}" +
            $"&clientSecret={Uri.EscapeDataString(clientSecret)}";

        return new PaymobIntentionResult(intentionId, orderId, clientSecret, checkoutUrl);
    }

    public bool VerifyTransactionHmac(IReadOnlyDictionary<string, string> flatFields, string receivedHmac)
    {
        if (string.IsNullOrWhiteSpace(_options.HmacSecret) || string.IsNullOrWhiteSpace(receivedHmac))
        {
            return false;
        }

        string Val(string key) =>
            flatFields.TryGetValue(key, out var v) ? (v ?? string.Empty) : string.Empty;

        // Paymob HMAC concat order
        var concatenated =
            Val("amount_cents") +
            Val("created_at") +
            Val("currency") +
            Val("error_occured") +
            Val("has_parent_transaction") +
            Val("id") +
            Val("integration_id") +
            Val("is_3d_secure") +
            Val("is_auth") +
            Val("is_capture") +
            Val("is_refunded") +
            Val("is_standalone_payment") +
            Val("is_voided") +
            Val("order_id") +
            Val("owner") +
            Val("pending") +
            Val("source_data.pan") +
            Val("source_data.sub_type") +
            Val("source_data.type") +
            Val("success");

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.HmacSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenated));
        var calculated = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(calculated),
            Encoding.UTF8.GetBytes(receivedHmac.Trim().ToLowerInvariant()));
    }

    private static string Combine(string baseUrl, string path)
    {
        return baseUrl.TrimEnd('/') + path;
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p)) return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => p.ToString()
        };
    }

    private static string? GetNestedString(JsonElement el, string parent, string child)
    {
        if (!el.TryGetProperty(parent, out var p) || p.ValueKind != JsonValueKind.Object) return null;
        return GetString(p, child);
    }
}
