using System.ComponentModel.DataAnnotations;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Services;

public record CreateProblemDto(
    int UserId,
    [property: Required(ErrorMessage = "Problem title is required.")]
    [property: StringLength(200, MinimumLength = 3, ErrorMessage = "Problem title must be between 3 and 200 characters.")]
    string Title,

    [property: Required(ErrorMessage = "Problem description is required.")]
    [property: StringLength(10000, MinimumLength = 10, ErrorMessage = "Problem description must be between 10 and 10000 characters.")]
    string Description,

    [property: StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    string? ErrorMessage = null,

    [property: StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    string? StackTrace = null,

    [property: StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    string? SourceCode = null,

    [property: StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    string? Logs = null,

    [property: StringLength(100, ErrorMessage = "Framework cannot exceed 100 characters.")]
    string? Framework = null
);

public record AddProblemRequestDto(
    [property: Required(ErrorMessage = "Request content is required.")]
    [property: StringLength(10000, MinimumLength = 5, ErrorMessage = "Request content must be between 5 and 10000 characters.")]
    string Content,

    [property: StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    string? ErrorMessage = null,

    [property: StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    string? StackTrace = null,

    [property: StringLength(100, ErrorMessage = "TechnologyHint cannot exceed 100 characters.")]
    string? TechnologyHint = null
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
    [property: Range(1, int.MaxValue, ErrorMessage = "AttemptId must be a positive integer.")]
    int AttemptId,

    bool IsSolved,

    [property: StringLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    string? Comments = null
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
    Task<ProblemRequestDto> AddProblemRequestAsync(int problemId, AddProblemRequestDto dto, int? userId = null, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> DebugProblemAsync(int problemId, int requestId, int? userId = null, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> SubmitFeedbackAsync(int problemId, int attemptId, bool isSolved, string? comments, int? userId = null, CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemDetailsAsync(int problemId, CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemByCodeAsync(string problemCode, CancellationToken cancellationToken = default);
    Task<ProblemHistoryDto> GetProblemHistoryAsync(int problemId, CancellationToken cancellationToken = default);
    Task<PagedResultDto<ProblemSearchResultDto>> SearchProblemsAsync(string? query, string? technology, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
}
