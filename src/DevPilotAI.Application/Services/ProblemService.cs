using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Services;

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
        var count = await _dbContext.Problems.CountAsync(cancellationToken) + 1;
        var problemCode = $"DEV-{count:D6}";

        var problem = new Problem
        {
            ProblemCode = problemCode,
            UserId = dto.UserId,
            Title = dto.Title,
            Summary = dto.Description.Length > 200 ? dto.Description.Substring(0, 200) : dto.Description,
            Status = ProblemStatus.InProgress
        };

        _dbContext.Problems.Add(problem);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var request = new ProblemRequest
        {
            ProblemId = problem.ProblemId,
            Description = dto.Description,
            ErrorMessage = dto.ErrorMessage,
            StackTrace = dto.StackTrace,
            SourceCode = dto.SourceCode,
            Logs = dto.Logs,
            Framework = dto.Framework
        };

        _dbContext.ProblemRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var sysPrompt = "You are an expert AI software debugging agent. Analyze the problem details, explain the root cause, and present a clear step-by-step solution.";
        var userPrompt = $"Title: {dto.Title}\nFramework: {dto.Framework}\nDescription: {dto.Description}\nError: {dto.ErrorMessage}\nStackTrace: {dto.StackTrace}\nCode: {dto.SourceCode}";

        var aiResponse = await _aiGateway.ExecuteAsync(new AIGatewayRequest(
            SystemPrompt: sysPrompt,
            UserPrompt: userPrompt,
            TaskType: "Debugging",
            UserId: dto.UserId
        ), cancellationToken);

        var attempt = new DebuggingAttempt
        {
            ProblemId = problem.ProblemId,
            ProblemRequestId = request.ProblemRequestId,
            RootCauseAnalysis = "Probable root cause identified from error signature.",
            Explanation = "Detailed diagnostic explanation.",
            ProposedSolution = aiResponse.Content
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

    public async Task<DebuggingResultDto> SubmitFeedbackAsync(int problemId, int attemptId, bool isSolved, string? comments, CancellationToken cancellationToken = default)
    {
        var problem = await _dbContext.Problems.FirstOrDefaultAsync(p => p.ProblemId == problemId, cancellationToken)
            ?? throw new KeyNotFoundException($"Problem {problemId} not found");

        var attempt = await _dbContext.DebuggingAttempts.FirstOrDefaultAsync(a => a.DebuggingAttemptId == attemptId, cancellationToken)
            ?? throw new KeyNotFoundException($"Debugging attempt {attemptId} not found");

        var feedback = new DeveloperFeedback
        {
            DebuggingAttemptId = attemptId,
            FeedbackResult = isSolved ? AttemptResult.Solved : AttemptResult.StillHaveProblem,
            AdditionalComments = comments
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
                DetailedSteps = attempt.ProposedSolution
            };

            _dbContext.ProblemSolutions.Add(solution);

            var articleCode = $"ART-{problem.ProblemCode}";
            var article = new KnowledgeArticle
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

            var version = new ArticleVersion
            {
                KnowledgeArticleId = article.KnowledgeArticleId,
                VersionNumber = 1,
                Title = article.Title,
                ContentMarkdown = $"# {article.Title}\n\n## Root Cause\n{attempt.RootCauseAnalysis}\n\n## Solution\n{attempt.ProposedSolution}",
                ChangeReason = "Initial resolution synthesis"
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
            ProviderUsed: "Completed"
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
}
