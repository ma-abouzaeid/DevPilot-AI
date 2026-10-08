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

public record AddProblemRequestDto(
    string Content,
    string? ErrorMessage,
    string? StackTrace,
    string? TechnologyHint
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

public record FeedbackRequestDto(
    int AttemptId,
    bool IsSolved,
    string? Comments
);

public record ProblemRequestDto(
    int ProblemRequestId,
    string Description,
    string? ErrorMessage,
    string? StackTrace,
    string? SourceCode,
    string? Logs,
    string? Framework,
    DateTime CreatedAt
);

public record DebuggingAttemptDto(
    int DebuggingAttemptId,
    int ProblemRequestId,
    int AttemptNumber,
    string RootCause,
    string Explanation,
    string ProposedSolution,
    DateTime CreatedAt
);

public record ProblemSolutionDto(
    int SolutionId,
    int SuccessfulAttemptId,
    string RootCause,
    string Summary,
    string DetailedSteps
);

public record ProblemHistoryDto(
    int ProblemId,
    string ProblemCode,
    string Title,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    List<ProblemRequestDto> Requests,
    List<DebuggingAttemptDto> Attempts,
    List<ProblemSolutionDto> Solutions
);

public record ProblemSearchResultDto(
    int ProblemId,
    string ProblemCode,
    string Title,
    string Summary,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record PagedResultDto<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public interface IProblemService
{
    Task<DebuggingResultDto> SubmitProblemAsync(CreateProblemDto dto, CancellationToken cancellationToken = default);
    Task<ProblemRequestDto> AddProblemRequestAsync(int problemId, AddProblemRequestDto dto, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> DebugProblemAsync(int problemId, int requestId, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> SubmitFeedbackAsync(int problemId, int attemptId, bool isSolved, string? comments, CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemDetailsAsync(int problemId, CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemByCodeAsync(string problemCode, CancellationToken cancellationToken = default);
    Task<ProblemHistoryDto> GetProblemHistoryAsync(int problemId, CancellationToken cancellationToken = default);
    Task<PagedResultDto<ProblemSearchResultDto>> SearchProblemsAsync(string? query, string? technology, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
}
