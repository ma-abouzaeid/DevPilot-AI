using System.Text.Json;
using DevPilotAI.Application.Challenges;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Plans;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Problems;

public class ProblemService : IProblemService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAIGateway _aiGateway;
    private readonly ISimilarityEngine? _similarityEngine;
    private readonly IChallengeService? _challengeService;
    private readonly IPlanUsageService? _planUsageService;
    private readonly IAuditService? _auditService;

    public ProblemService(
        IApplicationDbContext dbContext,
        IAIGateway aiGateway,
        ISimilarityEngine? similarityEngine = null,
        IChallengeService? challengeService = null,
        IPlanUsageService? planUsageService = null,
        IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _aiGateway = aiGateway;
        _similarityEngine = similarityEngine;
        _challengeService = challengeService;
        _planUsageService = planUsageService;
        _auditService = auditService;
    }

    public async Task<DebuggingResultDto> SubmitProblemAsync(CreateProblemDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.SourceCode))
        {
            throw AppError.InvalidRequest("Source code is required.", "INVALID_REQUEST", nameof(dto.SourceCode));
        }

        if (string.IsNullOrWhiteSpace(dto.ErrorMessage))
        {
            throw AppError.InvalidRequest("Error message is required.", "INVALID_REQUEST", nameof(dto.ErrorMessage));
        }

        if (dto.UserId > 0 && await _dbContext.Users.AnyAsync(cancellationToken))
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.UserId == dto.UserId, cancellationToken);
            if (!userExists)
            {
                throw AppError.NotFound($"User with ID {dto.UserId} was not found.");
            }
        }

        var errorMessage = dto.ErrorMessage.Trim();
        var sourceCode = dto.SourceCode.Trim();
        var description = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Error reported while running the submitted code:\n{errorMessage}"
            : dto.Description.Trim();
        var provisionalTitle = string.IsNullOrWhiteSpace(dto.Title)
            ? BuildProvisionalTitle(errorMessage)
            : dto.Title.Trim();

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
                Title = provisionalTitle,
                Summary = description.Length > 200 ? description[..200].Trim() : description,
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
            Description = description,
            ErrorMessage = errorMessage,
            StackTrace = dto.StackTrace?.Trim(),
            SourceCode = sourceCode,
            Logs = dto.Logs?.Trim(),
            Framework = dto.Framework?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ProblemRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await DebugProblemInternalAsync(problem, request, cancellationToken);
    }

    private static string BuildProvisionalTitle(string errorMessage)
    {
        var compact = string.Join(' ', errorMessage.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(compact))
        {
            return "Debug request";
        }

        return compact.Length <= 120 ? compact : compact[..117].TrimEnd() + "...";
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
            SourceCode = dto.SourceCode?.Trim(),
            Logs = dto.Logs?.Trim(),
            Framework = dto.TechnologyHint?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        problem.Status = ProblemStatus.InProgress;
        problem.ResolvedAt = null;

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

    public async Task<DebuggingResultDto> FollowUpProblemAsync(int problemId, FollowUpProblemDto dto, int userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.SourceCode))
        {
            throw AppError.InvalidRequest("Source code is required.", "INVALID_REQUEST", nameof(dto.SourceCode));
        }

        if (string.IsNullOrWhiteSpace(dto.ErrorMessage))
        {
            throw AppError.InvalidRequest("Error message is required.", "INVALID_REQUEST", nameof(dto.ErrorMessage));
        }

        var problem = await _dbContext.Problems
            .FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Problem with ID {problemId} was not found.");

        if (problem.UserId != userId)
        {
            throw AppError.Forbidden("You do not have permission to follow up on this problem.");
        }

        var errorMessage = dto.ErrorMessage.Trim();
        var sourceCode = dto.SourceCode.Trim();
        var description = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Follow-up: still reproducing the issue.\n{errorMessage}"
            : dto.Description.Trim();

        var request = new ProblemRequest
        {
            ProblemId = problem.ProblemId,
            Description = description,
            ErrorMessage = errorMessage,
            SourceCode = sourceCode,
            CreatedAt = DateTime.UtcNow
        };

        problem.Status = ProblemStatus.InProgress;
        problem.ResolvedAt = null;

        _dbContext.ProblemRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await DebugProblemInternalAsync(problem, request, cancellationToken);
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

        if (!isSolved)
        {
            problem.Status = ProblemStatus.InProgress;
            problem.ResolvedAt = null;
        }
        else
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
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_challengeService != null)
            {
                try
                {
                    await _challengeService.GenerateFromSolvedProblemAsync(problem, article, attempt, cancellationToken);
                }
                catch
                {
                    // Challenge generation must not block marking a problem as solved.
                }
            }

            if (_auditService != null)
            {
                await _auditService.LogAsync(
                    SystemAuditEventTypes.ProblemSolved,
                    nameof(Problem),
                    problem.ProblemId.ToString(),
                    oldValues: null,
                    newValues: new
                    {
                        problem.ProblemId,
                        problem.ProblemCode,
                        AttemptId = attempt.DebuggingAttemptId,
                        ArticleId = article.KnowledgeArticleId
                    },
                    userId: problem.UserId,
                    cancellationToken: cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var payload = DebuggingResultParser.ParseOrFallback(
            attempt.ProposedSolution,
            problem.Title);

        // Prefer persisted root-cause/explanation when feedback path reuses an attempt.
        if (!string.IsNullOrWhiteSpace(attempt.RootCauseAnalysis))
        {
            payload.RootCause = attempt.RootCauseAnalysis;
        }

        var dto = DebuggingResultParser.ToDto(
            problem.ProblemId,
            problem.ProblemCode,
            attemptId,
            isSolved ? "Resolution Confirmed" : "Follow-up Required",
            payload);

        return dto with
        {
            ProposedSolution = attempt.ProposedSolution
        };
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

    public async Task<ProblemHistoryDto> GetProblemHistoryAsync(int problemId, int? viewerUserId = null, CancellationToken cancellationToken = default)
    {
        var problem = await _dbContext.Problems
            .Include(p => p.ProblemRequests)
            .Include(p => p.DebuggingAttempts)
            .Include(p => p.ProblemSolutions)
            .FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken)
            ?? throw AppError.NotFound($"Problem with ID {problemId} was not found.");

        var isOwner = viewerUserId.HasValue && problem.UserId == viewerUserId.Value;
        var includeSourceCode = isOwner;

        var requests = problem.ProblemRequests
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ProblemRequestDto(
                r.ProblemRequestId,
                r.Description,
                r.ErrorMessage,
                r.StackTrace,
                includeSourceCode ? r.SourceCode : null,
                includeSourceCode ? r.Logs : null,
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
            Solutions: solutions,
            IsOwner: isOwner,
            Summary: problem.Summary
        );
    }

    public async Task<PlatformStatsDto> GetPlatformStatsAsync(CancellationToken cancellationToken = default)
    {
        var developers = await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        var problemsSolved = await _dbContext.Problems.AsNoTracking()
            .CountAsync(p => p.Status == ProblemStatus.Solved, cancellationToken);
        var knowledgeArticles = await _dbContext.KnowledgeArticles.AsNoTracking()
            .CountAsync(a => a.IsPublished, cancellationToken);
        var codingChallenges = await _dbContext.Challenges.AsNoTracking()
            .CountAsync(c => c.IsPublished && !c.IsDeleted, cancellationToken);

        return new PlatformStatsDto(
            Developers: developers,
            ProblemsSolved: problemsSolved,
            KnowledgeArticles: knowledgeArticles,
            CodingChallenges: codingChallenges
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

    public async Task<MyProblemsDashboardDto> GetMyProblemsDashboardAsync(int userId, int recentCount = 8, CancellationToken cancellationToken = default)
    {
        if (recentCount < 1 || recentCount > 50) recentCount = 8;

        var mine = _dbContext.Problems.AsNoTracking().Where(p => p.UserId == userId);
        var totalCount = await mine.CountAsync(cancellationToken);
        var solvedCount = await mine.CountAsync(p => p.Status == ProblemStatus.Solved, cancellationToken);
        var openCount = totalCount - solvedCount;

        var recent = await mine
            .OrderByDescending(p => p.CreatedAt)
            .Take(recentCount)
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

        return new MyProblemsDashboardDto(
            OpenCount: openCount,
            SolvedCount: solvedCount,
            TotalCount: totalCount,
            Recent: recent
        );
    }

    public async Task<PagedResultDto<KnowledgeArticleListItemDto>> SearchPublishedArticlesAsync(
        string? query,
        string? technology,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var dbQuery = _dbContext.KnowledgeArticles
            .AsNoTracking()
            .Where(a => a.IsPublished && a.Problem.Status == ProblemStatus.Solved);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            dbQuery = dbQuery.Where(a =>
                a.Title.ToLower().Contains(q) ||
                a.Summary.ToLower().Contains(q) ||
                a.ArticleCode.ToLower().Contains(q) ||
                a.Problem.ProblemCode.ToLower().Contains(q) ||
                a.Problem.Title.ToLower().Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(technology))
        {
            var tech = technology.Trim().ToLower();
            dbQuery = dbQuery.Where(a =>
                a.Problem.ProblemRequests.Any(r => r.Framework != null && r.Framework.ToLower().Contains(tech)));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderByDescending(a => a.Problem.ResolvedAt ?? a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new KnowledgeArticleListItemDto(
                a.KnowledgeArticleId,
                a.ProblemId,
                a.ArticleCode,
                a.Problem.ProblemCode,
                a.Title,
                a.Summary,
                a.Problem.ProblemRequests
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => r.Framework)
                    .FirstOrDefault(),
                a.CreatedAt,
                a.Problem.ResolvedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<KnowledgeArticleListItemDto>(
            Items: items,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize
        );
    }

    public async Task<KnowledgeArticleDetailsDto?> GetPublishedArticleAsync(
        int? knowledgeArticleId = null,
        int? problemId = null,
        string? articleOrProblemCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.KnowledgeArticles
            .AsNoTracking()
            .Include(a => a.Problem)
                .ThenInclude(p => p.ProblemRequests)
            .Include(a => a.Problem)
                .ThenInclude(p => p.ProblemSolutions)
            .Include(a => a.Versions)
            .Where(a => a.IsPublished);

        if (knowledgeArticleId.HasValue)
        {
            query = query.Where(a => a.KnowledgeArticleId == knowledgeArticleId.Value);
        }
        else if (problemId.HasValue)
        {
            query = query.Where(a => a.ProblemId == problemId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(articleOrProblemCode))
        {
            var code = articleOrProblemCode.Trim();
            query = query.Where(a =>
                a.ArticleCode == code ||
                a.Problem.ProblemCode == code ||
                a.ArticleCode == $"ART-{code}");
        }
        else
        {
            return null;
        }

        var article = await query.FirstOrDefaultAsync(cancellationToken);
        if (article == null)
        {
            return null;
        }

        var version = article.Versions
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        var solution = article.Problem.ProblemSolutions
            .OrderByDescending(s => s.SolvedAt)
            .FirstOrDefault();

        var framework = article.Problem.ProblemRequests
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.Framework)
            .FirstOrDefault();

        var challengeId = await _dbContext.Challenges.AsNoTracking()
            .Where(c => c.KnowledgeArticleId == article.KnowledgeArticleId && c.IsPublished && !c.IsDeleted)
            .Select(c => (int?)c.ChallengeId)
            .FirstOrDefaultAsync(cancellationToken);

        return new KnowledgeArticleDetailsDto(
            KnowledgeArticleId: article.KnowledgeArticleId,
            ProblemId: article.ProblemId,
            ArticleCode: article.ArticleCode,
            ProblemCode: article.Problem.ProblemCode,
            Title: version?.Title ?? article.Title,
            Summary: article.Summary,
            ContentMarkdown: version?.ContentMarkdown ?? article.Summary,
            Framework: framework,
            Status: article.Problem.Status.ToString(),
            CreatedAt: article.CreatedAt,
            ResolvedAt: article.Problem.ResolvedAt,
            RootCause: solution?.RootCause,
            SolutionSummary: solution?.Summary,
            ChallengeId: challengeId
        );
    }

    private async Task<DebuggingResultDto> DebugProblemInternalAsync(Problem problem, ProblemRequest request, CancellationToken cancellationToken)
    {
        var attemptCount = await _dbContext.DebuggingAttempts
            .CountAsync(a => a.ProblemId == problem.ProblemId, cancellationToken) + 1;

        var queryText = $"{problem.Title}\n{request.Framework}\n{request.Description}\n{request.ErrorMessage}\n{request.StackTrace}\n{request.SourceCode}";

        SimilarityMatchResult? bestMatch = null;

        if (_similarityEngine != null)
        {
            try
            {
                bestMatch = await TryCaptureSimilarityAsync(request, queryText, cancellationToken);
            }
            catch (Exception)
            {
                // Similarity is an optimization only. Missing embedding tables or match failures
                // must never block AI debugging or leave the ticket without a solution.
                DetachPendingSimilarityEntities(request.ProblemRequestId);
                bestMatch = null;
            }
        }

        string taskType;
        string? existingSolution = null;
        string? similarityNote = null;

        if (bestMatch != null)
        {
            taskType = "KnowledgeAdaptation";
            existingSolution = $"From {bestMatch.MatchedProblemCode} - {bestMatch.MatchedTitle} (similarity {bestMatch.SimilarityScore:P0}):\n{bestMatch.ProvenSolution}";
            similarityNote = $"Reused Knowledge from {bestMatch.MatchedProblemCode} (Similarity: {bestMatch.SimilarityScore:P0}).";
        }
        else
        {
            taskType = "Debugging";
        }

        var sysPrompt = DebuggingResultParser.BuildSystemPrompt();
        if (bestMatch != null)
        {
            sysPrompt += "\nA verified similar solution is provided. Adapt it to the current developer context while still returning the same JSON schema.";
        }

        var userPrompt = DebuggingResultParser.BuildUserPrompt(
            problem.Title,
            request.Framework,
            request.Description,
            request.ErrorMessage,
            request.StackTrace,
            request.SourceCode,
            request.Logs,
            existingSolution);

        var creditCost = CreditCosts.ForTaskType(taskType);
        if (_planUsageService != null)
        {
            await _planUsageService.EnsureFeatureAsync(problem.UserId, PlanFeatureKeys.AiDebugging, cancellationToken);
            await _planUsageService.EnsureCreditsAsync(problem.UserId, creditCost, cancellationToken);
        }

        var aiResponse = await _aiGateway.ExecuteAsync(new AIGatewayRequest(
            SystemPrompt: sysPrompt,
            UserPrompt: userPrompt,
            TaskType: taskType,
            UserId: problem.UserId,
            ProblemId: problem.ProblemId,
            Purpose: $"{taskType} for {problem.ProblemCode}: {TruncateForAudit(problem.Title, 120)}",
            MaxTokens: 4096,
            Temperature: 0.2
        ), cancellationToken);

        if (!aiResponse.IsSuccess)
        {
            throw AppError.ServiceUnavailable(
                aiResponse.ErrorMessage ?? "All AI providers failed to analyze this problem.",
                "AI_GATEWAY_UNAVAILABLE");
        }

        if (_planUsageService != null)
        {
            await _planUsageService.ChargeCreditsAsync(
                problem.UserId,
                creditCost,
                $"AI {taskType} for problem {problem.ProblemCode}",
                referenceId: $"problem:{problem.ProblemId}:{taskType}",
                aiUsageEventId: aiResponse.AIUsageEventId,
                metadataJson: JsonSerializer.Serialize(new
                {
                    taskType,
                    problem.ProblemId,
                    problem.ProblemCode,
                    provider = aiResponse.ProviderName,
                    model = aiResponse.ModelName,
                    aiRequestId = aiResponse.AIRequestId,
                    correlationId = aiResponse.CorrelationId
                }),
                tokensUsed: aiResponse.TotalTokens,
                cancellationToken: cancellationToken);
        }

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SystemAuditEventTypes.ProblemSubmitted,
                nameof(Problem),
                problem.ProblemId.ToString(),
                oldValues: null,
                newValues: new
                {
                    problem.ProblemId,
                    problem.ProblemCode,
                    TaskType = taskType,
                    aiResponse.ProviderName,
                    aiResponse.ModelName,
                    aiResponse.AIRequestId,
                    aiResponse.AIUsageEventId,
                    Credits = creditCost,
                    Tokens = aiResponse.TotalTokens
                },
                userId: problem.UserId,
                cancellationToken: cancellationToken);
        }

        var payload = DebuggingResultParser.ParseOrFallback(aiResponse.Content, problem.Title);
        if (!string.IsNullOrWhiteSpace(similarityNote) && bestMatch != null)
        {
            payload.Notes = string.IsNullOrWhiteSpace(payload.Notes)
                ? similarityNote
                : $"{similarityNote} {payload.Notes}";
            var rootCause = payload.RootCause ?? string.Empty;
            if (!rootCause.Contains(bestMatch.MatchedProblemCode, StringComparison.OrdinalIgnoreCase))
            {
                payload.RootCause = $"{similarityNote} {rootCause}".Trim();
            }
        }

        // Persist AI-inferred metadata so the user only needed code + error.
        var inferredTitle = !string.IsNullOrWhiteSpace(payload.Title)
            ? payload.Title.Trim()
            : payload.Problem?.Trim();
        if (!string.IsNullOrWhiteSpace(inferredTitle))
        {
            problem.Title = inferredTitle.Length <= 200 ? inferredTitle : inferredTitle[..197].TrimEnd() + "...";
            problem.Summary = string.IsNullOrWhiteSpace(payload.SolutionSummary)
                ? problem.Summary
                : (payload.SolutionSummary.Length > 200
                    ? payload.SolutionSummary[..200].Trim()
                    : payload.SolutionSummary.Trim());
        }

        if (!string.IsNullOrWhiteSpace(payload.Framework))
        {
            request.Framework = payload.Framework.Trim();
            if (request.Framework.Length > 100)
            {
                request.Framework = request.Framework[..100];
            }
        }

        var structuredDtoPreview = DebuggingResultParser.ToDto(
            problem.ProblemId,
            problem.ProblemCode,
            debuggingAttemptId: 0,
            aiResponse.ProviderName,
            payload);

        var attempt = new DebuggingAttempt
        {
            ProblemId = problem.ProblemId,
            ProblemRequestId = request.ProblemRequestId,
            AttemptNumber = attemptCount,
            RootCauseAnalysis = structuredDtoPreview.RootCause,
            Explanation = $"{structuredDtoPreview.Problem}\nLocation: {structuredDtoPreview.Location}",
            ProposedSolution = structuredDtoPreview.ProposedSolution,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DebuggingAttempts.Add(attempt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return structuredDtoPreview with
        {
            DebuggingAttemptId = attempt.DebuggingAttemptId,
            ProviderUsed = aiResponse.ProviderName
        };
    }

    private void DetachPendingSimilarityEntities(int problemRequestId)
    {
        if (_dbContext is not DbContext ef)
        {
            return;
        }

        foreach (var entry in ef.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                     .ToList())
        {
            if (entry.Entity is ProblemEmbedding embedding
                && embedding.ProblemRequestId == problemRequestId)
            {
                entry.State = EntityState.Detached;
            }
            else if (entry.Entity is SimilarityMatch match
                     && match.ProblemRequestId == problemRequestId)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private async Task<SimilarityMatchResult?> TryCaptureSimilarityAsync(
        ProblemRequest request,
        string queryText,
        CancellationToken cancellationToken)
    {
        var embeddingData = await _similarityEngine!.GenerateEmbeddingAsync(queryText, cancellationToken);
        if (embeddingData.Length > 0)
        {
            var existingEmbedding = await _dbContext.ProblemEmbeddings
                .FirstOrDefaultAsync(e => e.ProblemRequestId == request.ProblemRequestId, cancellationToken);

            if (existingEmbedding == null)
            {
                _dbContext.ProblemEmbeddings.Add(new ProblemEmbedding
                {
                    ProblemRequestId = request.ProblemRequestId,
                    EmbeddingData = embeddingData,
                    ModelVersion = "feature-v1",
                    CreatedAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var bestMatch = await _similarityEngine.FindBestMatchAsync(
            request.ProblemRequestId,
            queryText,
            request.Framework,
            threshold: 0.85,
            cancellationToken: cancellationToken);

        if (bestMatch != null)
        {
            _dbContext.SimilarityMatches.Add(new SimilarityMatch
            {
                ProblemRequestId = request.ProblemRequestId,
                MatchedProblemId = bestMatch.MatchedProblemId,
                SimilarityScore = (decimal)bestMatch.SimilarityScore,
                CreatedAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return bestMatch;
    }

    private static string TruncateForAudit(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
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
