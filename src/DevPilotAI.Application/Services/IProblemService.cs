using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Services;

public record CreateProblemDto(
    int UserId,
    string Title,
    string Description,
    string? ErrorMessage,
    string? StackTrace,
    string? SourceCode,
    string? Logs,
    string? Framework
);

public record DebuggingResultDto(
    int ProblemId,
    string ProblemCode,
    int DebuggingAttemptId,
    string RootCause,
    string Explanation,
    string ProposedSolution,
    string ProviderUsed
);

public interface IProblemService
{
    Task<DebuggingResultDto> SubmitProblemAsync(CreateProblemDto dto, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> SubmitFeedbackAsync(int problemId, int attemptId, bool isSolved, string? comments, CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemDetailsAsync(int problemId, CancellationToken cancellationToken = default);
}
