using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Challenges.DTOs;

public record ChallengeListItemDto(
    int ChallengeId,
    string Title,
    string Description,
    string Difficulty,
    int MaxPoints,
    DateTime CreatedAt,
    string? ProblemCode,
    bool SolvedByCurrentUser,
    int? BestPointsForCurrentUser
);

public record ChallengeDetailsDto(
    int ChallengeId,
    string Title,
    string Description,
    string Requirements,
    string StarterCode,
    string Difficulty,
    int MaxPoints,
    DateTime CreatedAt,
    string? ProblemCode,
    int? KnowledgeArticleId,
    bool SolvedByCurrentUser,
    int? BestPointsForCurrentUser
);

public class SubmitChallengeSolutionDto
{
    [Required]
    [MinLength(3)]
    [StringLength(50000)]
    public string SubmittedCode { get; set; } = string.Empty;
}

public record ChallengeEvaluationResultDto(
    int ChallengeAttemptId,
    int ChallengeId,
    bool IsPassed,
    int CorrectnessScore,
    int CodeQualityScore,
    int SecurityScore,
    int PointsAwarded,
    int MaxPoints,
    string FeedbackSummary,
    string Advice,
    string DetailedReport,
    string ProviderUsed
);

public record LeaderboardEntryDto(
    int Rank,
    int UserId,
    string DisplayName,
    int TotalPoints,
    int ChallengesSolved
);

public record MyChallengeStatsDto(
    int TotalPoints,
    int ChallengesSolved,
    int? Rank,
    IReadOnlyList<LeaderboardEntryDto> Top
);
