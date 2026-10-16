using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Problems.DTOs;

namespace DevPilotAI.Application.Problems;

public class ProblemService : IProblemService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAIGateway _aiGateway;

    public ProblemService(IApplicationDbContext dbContext, IAIGateway aiGateway)
    {
        _dbContext = dbContext;
        _aiGateway = aiGateway;
    }

    public async Task<DebuggingResultDto> SubmitProblemAsync(CreateProblemDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            throw AppError.InvalidRequest("Problem title is required.", "INVALID_REQUEST", nameof(dto.Title));
        }

        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            throw AppError.InvalidRequest("Problem description is required.", "INVALID_REQUEST", nameof(dto.Description));
        }

        if (dto.UserId > 0 && await _dbContext.Users.AnyAsync(cancellationToken))
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.UserId == dto.UserId, cancellationToken);
            if (!userExists)
            {
                throw AppError.NotFound($"User with ID {dto.UserId} was not found.");
            }
        }

        Problem? problem = null;
        const int maxRetries = 5;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var count = await _dbContext.Problems.IgnoreQueryFilters().CountAsync(cancellationToken) + 1;
            var problemCode = $"DEV-{count:D6}";

            while (await _dbContext.Problems.IgnoreQueryFilters().AnyAsync(p => p.ProblemCode == problemCode, cancellationToken))
            {
                count++;
                problemCode = $"DEV-{count:D6}";
            }

            problem = new Problem
            {
                ProblemCode = problemCode,
                UserId = dto.UserId,
                Title = dto.Title.Trim(),
                Summary = dto.Description.Length > 200 ? dto.Description.Substring(0, 200).Trim() : dto.Description.Trim(),
                Status = ProblemStatus.InProgress,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Problems.Add(problem);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < maxRetries)
            {
                if (_dbContext is DbContext efContext)
                {
                    efContext.Entry(problem).State = EntityState.Detached;
                }
                else
                {
                    _dbContext.Problems.Remove(problem);
                }
                problem = null;
            }
        }

        if (problem == null)
        {
            throw AppError.Conflict("Could not generate a unique problem code due to concurrent activity. Please retry.", "RESOURCE_CONFLICT");
        }

        var request = new ProblemRequest
        {
            ProblemId = problem.ProblemId,
            Description = dto.Description.Trim(),
            ErrorMessage = dto.ErrorMessage?.Trim(),
            StackTrace = dto.StackTrace?.Trim(),
            SourceCode = dto.SourceCode?.Trim(),
            Logs = dto.Logs?.Trim(),
            Framework = dto.Framework?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProblemRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await DebugProblemInternalAsync(problem, request, cancellationToken);
    }

    public async Task<ProblemRequestDto> AddProblemRequestAsync(int problemId, AddProblemRequestDto dto, int? userId = null, CancellationToken cancellationToken = default)
    {
        var problem = await _dbContext.Problems
            .FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Problem with ID {problemId} was not found.");

        if (userId.HasValue && problem.UserId != userId.Value)
        {
            throw AppError.Forbidden("You do not have permission to modify this problem.");
        }

        if (string.IsNullOrWhiteSpace(dto.Content))
        {
            throw AppError.InvalidRequest("Request content is required.", "INVALID_REQUEST", nameof(dto.Content));
        }

        var request = new ProblemRequest
        {
            ProblemId = problem.ProblemId,
            Description = dto.Content.Trim(),
            ErrorMessage = dto.ErrorMessage?.Trim(),
            StackTrace = dto.StackTrace?.Trim(),
            Framework = dto.TechnologyHint?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProblemRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProblemRequestDto(
            ProblemRequestId: request.ProblemRequestId,
            Description: request.Description,
            ErrorMessage: request.ErrorMessage,
            StackTrace: request.StackTrace,
            SourceCode: request.SourceCode,
            Logs: request.Logs,
            Framework: request.Framework,
            CreatedAt: request.CreatedAt
        );
    }

    public async Task<DebuggingResultDto> DebugProblemAsync(int problemId, int requestId, int? userId = null, CancellationToken cancellationToken = default)
    {
        var problem = await _dbContext.Problems
            .FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Problem with ID {problemId} was not found.");

        if (userId.HasValue && problem.UserId != userId.Value)
        {
            throw AppError.Forbidden("You do not have permission to debug this problem.");
        }

        var request = await _dbContext.ProblemRequests
            .FirstOrDefaultAsync(r => r.ProblemRequestId == requestId && r.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Problem request with ID {requestId} was not found for problem {problemId}.");

        return await DebugProblemInternalAsync(problem, request, cancellationToken);
    }

    public async Task<DebuggingResultDto> SubmitFeedbackAsync(int problemId, int attemptId, bool isSolved, string? comments, int? userId = null, CancellationToken cancellationToken = default)
    {
        var problem = await _dbContext.Problems
            .FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Problem with ID {problemId} was not found.");

        if (userId.HasValue && problem.UserId != userId.Value)
        {
            throw AppError.Forbidden("You do not have permission to submit feedback for this problem.");
        }

        var attempt = await _dbContext.DebuggingAttempts
            .FirstOrDefaultAsync(a => a.DebuggingAttemptId == attemptId && a.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Debugging attempt with ID {attemptId} was not found for problem {problemId}.");

        var feedback = new DeveloperFeedback
        {
            DebuggingAttemptId = attemptId,
            FeedbackResult = isSolved ? AttemptResult.Solved : AttemptResult.StillHaveProblem,
            AdditionalComments = comments?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DeveloperFeedbacks.Add(feedback);

        if (isSolved)
        {
            problem.Status = ProblemStatus.Solved;
            problem.ResolvedAt = DateTime.UtcNow;

            var solution = new ProblemSolution
            {
                ProblemId = problemId,
                SuccessfulAttemptId = attemptId,
                RootCause = attempt.RootCauseAnalysis,
                Summary = problem.Title,
                DetailedSteps = attempt.ProposedSolution,
                SolvedAt = DateTime.UtcNow
            };

            _dbContext.ProblemSolutions.Add(solution);

            var articleCode = $"ART-{problem.ProblemCode}";
            var article = await _dbContext.KnowledgeArticles
                .FirstOrDefaultAsync(a => a.ProblemId == problemId, cancellationToken);

            if (article == null)
            {
                article = new KnowledgeArticle
                {
                    ProblemId = problemId,
                    ArticleCode = articleCode,
                    Title = $"How to fix: {problem.Title}",
                    Summary = problem.Summary,
                    CurrentVersion = 1,
                    IsPublished = true
                };
                _dbContext.KnowledgeArticles.Add(article);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                article.CurrentVersion++;
            }

            var version = new ArticleVersion
            {
                KnowledgeArticleId = article.KnowledgeArticleId,
                VersionNumber = article.CurrentVersion,
                Title = article.Title,
                ContentMarkdown = $"# {article.Title}\n\n## Root Cause\n{attempt.RootCauseAnalysis}\n\n## Solution\n{attempt.ProposedSolution}",
                ChangeReason = isSolved ? "Resolution confirmed by developer feedback" : "Updated resolution synthesis",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ArticleVersions.Add(version);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DebuggingResultDto(
            ProblemId: problem.ProblemId,
            ProblemCode: problem.ProblemCode,
            DebuggingAttemptId: attemptId,
            RootCause: attempt.RootCauseAnalysis,
            Explanation: attempt.Explanation,
            ProposedSolution: attempt.ProposedSolution,
            ProviderUsed: isSolved ? "Resolution Confirmed" : "Follow-up Required"
        );
    }

    public async Task<Problem?> GetProblemDetailsAsync(int problemId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Problems
            .Include(p => p.ProblemRequests)
            .Include(p => p.DebuggingAttempts)
            .Include(p => p.ProblemSolutions)
            .FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken);
    }

    public async Task<Problem?> GetProblemByCodeAsync(string problemCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(problemCode))
        {
            throw AppError.InvalidRequest("Problem code is required.", "INVALID_REQUEST", nameof(problemCode));
        }

        return await _dbContext.Problems
            .Include(p => p.ProblemRequests)
            .Include(p => p.DebuggingAttempts)
            .Include(p => p.ProblemSolutions)
            .FirstOrDefaultAsync(p => p.ProblemCode == problemCode.Trim(), cancellationToken);
    }

    public async Task<ProblemHistoryDto> GetProblemHistoryAsync(int problemId, CancellationToken cancellationToken = default)
    {
        var problem = await _dbContext.Problems
            .Include(p => p.ProblemRequests)
            .Include(p => p.DebuggingAttempts)
            .Include(p => p.ProblemSolutions)
            .FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Problem with ID {problemId} was not found.");

        var requests = problem.ProblemRequests
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ProblemRequestDto(
                r.ProblemRequestId,
                r.Description,
                r.ErrorMessage,
                r.StackTrace,
                r.SourceCode,
                r.Logs,
                r.Framework,
                r.CreatedAt
            )).ToList();

        var attempts = problem.DebuggingAttempts
            .OrderBy(a => a.CreatedAt)
            .Select(a => new DebuggingAttemptDto(
                a.DebuggingAttemptId,
                a.ProblemRequestId,
                a.AttemptNumber,
                a.RootCauseAnalysis,
                a.Explanation,
                a.ProposedSolution,
                a.CreatedAt
            )).ToList();

        var solutions = problem.ProblemSolutions
            .OrderBy(s => s.SolvedAt)
            .Select(s => new ProblemSolutionDto(
                s.ProblemSolutionId,
                s.SuccessfulAttemptId,
                s.RootCause,
                s.Summary,
                s.DetailedSteps
            )).ToList();

        return new ProblemHistoryDto(
            ProblemId: problem.ProblemId,
            ProblemCode: problem.ProblemCode,
            Title: problem.Title,
            Status: problem.Status.ToString(),
            CreatedAt: problem.CreatedAt,
            ResolvedAt: problem.ResolvedAt,
            Requests: requests,
            Attempts: attempts,
            Solutions: solutions
        );
    }

    public async Task<PagedResultDto<ProblemSearchResultDto>> SearchProblemsAsync(
        string? query,
        string? technology,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var dbQuery = _dbContext.Problems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            dbQuery = dbQuery.Where(p =>
                p.Title.ToLower().Contains(q) ||
                p.Summary.ToLower().Contains(q) ||
                p.ProblemCode.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(technology))
        {
            var tech = technology.Trim().ToLower();
            dbQuery = dbQuery.Where(p => p.ProblemRequests.Any(r => r.Framework != null && r.Framework.ToLower().Contains(tech)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProblemSearchResultDto(
                p.ProblemId,
                p.ProblemCode,
                p.Title,
                p.Summary,
                p.Status.ToString(),
                p.CreatedAt,
                p.ResolvedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProblemSearchResultDto>(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize
        );
    }

    private async Task<DebuggingResultDto> DebugProblemInternalAsync(Problem problem, ProblemRequest request, CancellationToken cancellationToken)
    {
        var attemptCount = await _dbContext.DebuggingAttempts
            .CountAsync(a => a.ProblemId == problem.ProblemId, cancellationToken) + 1;

        var sysPrompt = "You are an expert AI software debugging agent. Analyze the problem details, explain the root cause, and present a clear step-by-step solution.";
        var userPrompt = $"Title: {problem.Title}\nFramework: {request.Framework}\nDescription: {request.Description}\nError: {request.ErrorMessage}\nStackTrace: {request.StackTrace}\nCode: {request.SourceCode}";

        var aiResponse = await _aiGateway.ExecuteAsync(new AIGatewayRequest(
            SystemPrompt: sysPrompt,
            UserPrompt: userPrompt,
            TaskType: "Debugging",
            UserId: problem.UserId
        ), cancellationToken);

        var attempt = new DebuggingAttempt
        {
            ProblemId = problem.ProblemId,
            ProblemRequestId = request.ProblemRequestId,
            AttemptNumber = attemptCount,
            RootCauseAnalysis = "Probable root cause identified from error signature.",
            Explanation = "Detailed diagnostic explanation.",
            ProposedSolution = aiResponse.Content,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DebuggingAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DebuggingResultDto(
            ProblemId: problem.ProblemId,
            ProblemCode: problem.ProblemCode,
            DebuggingAttemptId: attempt.DebuggingAttemptId,
            RootCause: attempt.RootCauseAnalysis,
            Explanation: attempt.Explanation,
            ProposedSolution: attempt.ProposedSolution,
            ProviderUsed: aiResponse.ProviderName
        );
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner != null)
        {
            var numberProp = inner.GetType().GetProperty("Number");
            if (numberProp != null && numberProp.GetValue(inner) is int number && (number == 2601 || number == 2627))
            {
                return true;
            }

            var innerMsg = inner.Message;
            if (innerMsg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                innerMsg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                innerMsg.Contains("IX_", StringComparison.OrdinalIgnoreCase) ||
                innerMsg.Contains("ProblemCode", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var msg = ex.Message;
        return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("IX_", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("ProblemCode", StringComparison.OrdinalIgnoreCase);
    }
}
