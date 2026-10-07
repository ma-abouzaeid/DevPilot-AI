namespace DevPilotAI.Application.Common.Interfaces;

public record AIGatewayRequest(
    string SystemPrompt,
    string UserPrompt,
    string TaskType,
    int? UserId = null,
    int? DebuggingAttemptId = null,
    int MaxTokens = 2048,
    double Temperature = 0.3
);

public record AIGatewayResponse(
    bool IsSuccess,
    string Content,
    string ProviderName,
    string ModelName,
    int PromptTokens,
    int CompletionTokens,
    long DurationMs,
    string? ErrorMessage = null
);

public interface IAIGateway
{
    Task<AIGatewayResponse> ExecuteAsync(AIGatewayRequest request, CancellationToken cancellationToken = default);
}
