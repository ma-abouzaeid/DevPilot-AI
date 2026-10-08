using System.Net;
using System.Text.Json;
using DevPilotAI.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace DevPilotAI.Tests;

public class BrevoEmailServiceTests
{
    private readonly Mock<ILogger<BrevoEmailService>> _mockLogger;
    private readonly BrevoOptions _brevoOptions;
    private readonly FrontendOptions _frontendOptions;

    public BrevoEmailServiceTests()
    {
        _mockLogger = new Mock<ILogger<BrevoEmailService>>();
        _brevoOptions = new BrevoOptions
        {
            ApiKey = "test-brevo-api-key-12345",
            SenderEmail = "midohema94@gmail.com",
            SenderName = "DevPilot",
            BaseUrl = "https://api.brevo.com"
        };
        _frontendOptions = new FrontendOptions
        {
            BaseUrl = "https://devpilot.ai"
        };
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode responseStatus, Action<HttpRequestMessage, string?>? onRequest = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                var content = req.Content != null ? req.Content.ReadAsStringAsync().GetAwaiter().GetResult() : null;
                onRequest?.Invoke(req, content);
                return Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = responseStatus,
                    Content = new StringContent("{\"messageId\":\"<123456@smtp-relay.mailin.fr>\"}")
                });
            });

        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.brevo.com")
        };
    }

    [Fact]
    public async Task SendVerificationEmailAsync_ShouldSendCorrectBrevoPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedContent = null;

        var httpClient = CreateMockHttpClient(HttpStatusCode.Created, (req, content) =>
        {
            capturedRequest = req;
            capturedContent = content;
        });

        var service = new BrevoEmailService(
            httpClient,
            Options.Create(_brevoOptions),
            Options.Create(_frontendOptions),
            _mockLogger.Object);

        await service.SendVerificationEmailAsync("developer@example.com", "John Developer", "123456");

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedContent);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/v3/smtp/email", capturedRequest.RequestUri?.AbsolutePath);
        Assert.True(capturedRequest.Headers.Contains("api-key"));
        Assert.Equal("test-brevo-api-key-12345", capturedRequest.Headers.GetValues("api-key").First());

        using var jsonDoc = JsonDocument.Parse(capturedContent);
        var root = jsonDoc.RootElement;

        Assert.Equal("DevPilot", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("midohema94@gmail.com", root.GetProperty("sender").GetProperty("email").GetString());

        var toElement = root.GetProperty("to")[0];
        Assert.Equal("developer@example.com", toElement.GetProperty("email").GetString());
        Assert.Equal("John Developer", toElement.GetProperty("name").GetString());

        Assert.Equal("Verify your DevPilot AI account", root.GetProperty("subject").GetString());

        var htmlContent = root.GetProperty("htmlContent").GetString();
        Assert.NotNull(htmlContent);
        Assert.Contains("123456", htmlContent);
        Assert.Contains("John Developer", htmlContent);
        Assert.Contains("DevPilot", htmlContent);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_ShouldBuildTrustedUrlAndCorrectPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedContent = null;

        var httpClient = CreateMockHttpClient(HttpStatusCode.Created, (req, content) =>
        {
            capturedRequest = req;
            capturedContent = content;
        });

        var service = new BrevoEmailService(
            httpClient,
            Options.Create(_brevoOptions),
            Options.Create(_frontendOptions),
            _mockLogger.Object);

        await service.SendPasswordResetEmailAsync("reset-user@example.com", "Jane Doe", "secure-reset-token-xyz");

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedContent);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("/v3/smtp/email", capturedRequest.RequestUri?.AbsolutePath);
        Assert.True(capturedRequest.Headers.Contains("api-key"));

        using var jsonDoc = JsonDocument.Parse(capturedContent);
        var root = jsonDoc.RootElement;

        Assert.Equal("Reset your DevPilot AI password", root.GetProperty("subject").GetString());

        var htmlContent = root.GetProperty("htmlContent").GetString();
        Assert.NotNull(htmlContent);
        Assert.Contains("https://devpilot.ai/reset-password?token=secure-reset-token-xyz", htmlContent);
        Assert.Contains("Jane Doe", htmlContent);
    }

    [Fact]
    public async Task SendVerificationEmailAsync_WhenApiKeyEmpty_ShouldSkipWithoutError()
    {
        var optionsWithoutKey = new BrevoOptions
        {
            ApiKey = "",
            SenderEmail = "midohema94@gmail.com",
            SenderName = "DevPilot"
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.brevo.com")
        };

        var service = new BrevoEmailService(
            httpClient,
            Options.Create(optionsWithoutKey),
            Options.Create(_frontendOptions),
            _mockLogger.Object);

        await service.SendVerificationEmailAsync("recipient@example.com", "User", "654321");

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendTransactionalEmailAsync_WhenBrevoFails_ShouldHandleSafely()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized);

        var service = new BrevoEmailService(
            httpClient,
            Options.Create(_brevoOptions),
            Options.Create(_frontendOptions),
            _mockLogger.Object);

        var exception = await Record.ExceptionAsync(() =>
            service.SendVerificationEmailAsync("dev@example.com", "Dev", "999888"));

        Assert.Null(exception);
    }
}
