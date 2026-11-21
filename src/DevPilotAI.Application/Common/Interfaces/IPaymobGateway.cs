namespace DevPilotAI.Application.Common.Interfaces;

public record PaymobIntentionRequest(
    int AmountCents,
    string Currency,
    IReadOnlyList<int> PaymentMethods,
    string SpecialReference,
    string NotificationUrl,
    string RedirectionUrl,
    PaymobBillingData BillingData,
    string ItemName
);

public record PaymobBillingData(
    string FirstName,
    string LastName,
    string Email,
    string PhoneNumber
);

public record PaymobIntentionResult(
    string IntentionId,
    string? OrderId,
    string ClientSecret,
    string CheckoutUrl
);

public interface IPaymobGateway
{
    bool IsConfigured { get; }
    Task<PaymobIntentionResult> CreateIntentionAsync(PaymobIntentionRequest request, CancellationToken cancellationToken = default);
    bool VerifyTransactionHmac(IReadOnlyDictionary<string, string> flatFields, string receivedHmac);
}
