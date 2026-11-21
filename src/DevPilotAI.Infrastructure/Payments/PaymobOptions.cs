namespace DevPilotAI.Infrastructure.Payments;

public class PaymobOptions
{
    public const string SectionName = "Paymob";

    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string HmacSecret { get; set; } = string.Empty;
    public int IntegrationId { get; set; }
    public string BaseUrl { get; set; } = "https://accept.paymob.com";
    public string CheckoutBaseUrl { get; set; } = "https://eg.checkout.paymob.com";
    public decimal UsdToEgpRate { get; set; } = 50m;
    /// <summary>Public base URL of the API used for Paymob callbacks (e.g. https://devpilot-ai.runasp.net).</summary>
    public string WebhookBaseUrl { get; set; } = "http://devpilot-ai.runasp.net";
    /// <summary>Where the browser lands after payment (plans page).</summary>
    public string FrontendReturnUrl { get; set; } = "https://devpilot-ai-three.vercel.app/plans.html";
}
