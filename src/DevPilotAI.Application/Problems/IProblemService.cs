using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Problems;

public interface IProblemService
{
    Task<DebuggingResultDto> SubmitProblemAsync(CreateProblemDto dto, CancellationToken cancellationToken = default);
    Task<ProblemRequestDto> AddProblemRequestAsync(int problemId, AddProblemRequestDto dto, int? userId = null, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> FollowUpProblemAsync(int problemId, FollowUpProblemDto dto, int userId, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> DebugProblemAsync(int problemId, int requestId, int? userId = null, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> SubmitFeedbackAsync(int problemId, int attemptId, bool isSolved, string? comments, int? userId = null, CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemDetailsAsync(int problemId, CancellationToken cancellationToken = default);
    Task<Problem?> GetProblemByCodeAsync(string problemCode, CancellationToken cancellationToken = default);
    Task<ProblemHistoryDto> GetProblemHistoryAsync(int problemId, int? viewerUserId = null, CancellationToken cancellationToken = default);
    Task<PagedResultDto<ProblemSearchResultDto>> SearchProblemsAsync(string? query, string? technology, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<MyProblemsDashboardDto> GetMyProblemsDashboardAsync(int userId, int recentCount = 8, CancellationToken cancellationToken = default);
    Task<PagedResultDto<KnowledgeArticleListItemDto>> SearchPublishedArticlesAsync(string? query, string? technology, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListPublishedArticleTechnologiesAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeArticleDetailsDto?> GetPublishedArticleAsync(int? knowledgeArticleId = null, int? problemId = null, string? articleOrProblemCode = null, CancellationToken cancellationToken = default);
    Task<PlatformStatsDto> GetPlatformStatsAsync(CancellationToken cancellationToken = default);
}
