namespace DevPilotAI.Application.Problems.DTOs;

public record DebugStepDto(
    int Order,
    string Title,
    string Detail
);

/// <summary>Debugging payload; keep field order stable for the UI.</summary>
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
    // Old shape: one big proposed-solution string.
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
    List<ProblemSolutionDto> Solutions,
    bool IsOwner = false,
    string? Summary = null
);

public record PlatformStatsDto(
    int Developers,
    int ProblemsSolved,
    int KnowledgeArticles,
    int CodingChallenges
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

public record MyProblemsDashboardDto(
    int OpenCount,
    int SolvedCount,
    int TotalCount,
    IReadOnlyList<ProblemSearchResultDto> Recent
);

public record KnowledgeArticleListItemDto(
    int KnowledgeArticleId,
    int ProblemId,
    string ArticleCode,
    string ProblemCode,
    string Title,
    string Summary,
    string? Framework,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record KnowledgeArticleDetailsDto(
    int KnowledgeArticleId,
    int ProblemId,
    string ArticleCode,
    string ProblemCode,
    string Title,
    string Summary,
    string ContentMarkdown,
    string? Framework,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? RootCause,
    string? SolutionSummary,
    int? ChallengeId = null
);

public record ArticleCommentDto(
    long ArticleCommentId,
    int UserId,
    string UserName,
    string Body,
    DateTime CreatedAt,
    bool IsMine
);

public record ArticleEngagementDto(
    int KnowledgeArticleId,
    int LikesCount,
    int CommentsCount,
    int SavesCount,
    bool LikedByMe,
    bool SavedByMe,
    IReadOnlyList<ArticleCommentDto> Comments
);

public record ArticleToggleResultDto(
    bool Active,
    int Count
);
