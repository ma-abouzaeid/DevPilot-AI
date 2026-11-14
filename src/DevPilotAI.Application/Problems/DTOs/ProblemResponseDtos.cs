namespace DevPilotAI.Application.Problems.DTOs;

public record DebugStepDto(
    int Order,
    string Title,
    string Detail
);

/// <summary>
/// Fixed, ordered debugging result shown for every problem.
/// Field order is part of the product contract.
/// </summary>
public record DebuggingResultDto(
    int ProblemId,
    string ProblemCode,
    int DebuggingAttemptId,
    string Problem,
    string Location,
    string RootCause,
    string SolutionSummary,
    IReadOnlyList<DebugStepDto> Steps,
    string CorrectedCode,
    string? Notes,
    string ProviderUsed,
    /// <summary>Legacy/compat: full proposed solution text (summary + steps + code).</summary>
    string ProposedSolution
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
