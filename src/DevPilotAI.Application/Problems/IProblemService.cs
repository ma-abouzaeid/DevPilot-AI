using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Problems;

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
