using DevPilotAI.Application.Challenges.DTOs;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Challenges;

public interface IChallengeService
{
    Task<Challenge?> GenerateFromSolvedProblemAsync(
        Problem problem,
        KnowledgeArticle article,
        DebuggingAttempt successfulAttempt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChallengeListItemDto>> ListPublishedAsync(int? currentUserId = null, CancellationToken cancellationToken = default);
    Task<ChallengeDetailsDto?> GetByIdAsync(int challengeId, int? currentUserId = null, CancellationToken cancellationToken = default);
    Task<ChallengeEvaluationResultDto> SubmitSolutionAsync(int challengeId, int userId, SubmitChallengeSolutionDto dto, CancellationToken cancellationToken = default);
    Task<MyChallengeStatsDto> GetLeaderboardAsync(int? currentUserId = null, int top = 10, CancellationToken cancellationToken = default);
}
