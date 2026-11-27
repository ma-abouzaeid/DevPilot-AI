using System.Security.Claims;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Problems;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.General)]
public class ProblemsController : ControllerBase
{
    private readonly IProblemService _problemService;

    public ProblemsController(IProblemService problemService)
    {
        _problemService = problemService;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private int? TryGetAuthenticatedUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) && id > 0 ? id : null;
    }

    private int GetRequiredAuthenticatedUserId()
    {
        return TryGetAuthenticatedUserId()
            ?? throw AppError.Unauthorized("User identity claim is missing or invalid.");
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiAnalysis)]
    public async Task<IActionResult> SubmitProblem([FromBody] CreateProblemDto dto, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetRequiredAuthenticatedUserId();
        dto.UserId = authenticatedUserId;

        var result = await _problemService.SubmitProblemAsync(dto, cancellationToken);
        var response = ApiResponse<DebuggingResultDto>.SuccessResult(result, "Your problem has been submitted successfully.", GetTraceId());
        return CreatedAtAction(nameof(GetProblem), new { problemId = result.ProblemId }, response);
    }

    [HttpPost("{problemId:int}/requests")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiFollowUp)]
    public async Task<IActionResult> AddProblemRequest(int problemId, [FromBody] AddProblemRequestDto dto, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.AddProblemRequestAsync(problemId, dto, authenticatedUserId, cancellationToken);
        var response = ApiResponse<ProblemRequestDto>.SuccessResult(result, "Your follow-up request has been submitted successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("{problemId:int}/follow-up")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiAnalysis)]
    public async Task<IActionResult> FollowUpProblem(int problemId, [FromBody] FollowUpProblemDto dto, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.FollowUpProblemAsync(problemId, dto, authenticatedUserId, cancellationToken);
        var response = ApiResponse<DebuggingResultDto>.SuccessResult(result, "Follow-up analysis completed successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("{problemId:int}/debug")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiAnalysis)]
    public async Task<IActionResult> DebugProblem(int problemId, [FromQuery] int requestId, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.DebugProblemAsync(problemId, requestId, authenticatedUserId, cancellationToken);
        var response = ApiResponse<DebuggingResultDto>.SuccessResult(result, "AI debugging analysis completed successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpPost("{problemId:int}/attempts/{attemptId:int}/feedback")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiFollowUp)]
    public async Task<IActionResult> SubmitFeedback(int problemId, int attemptId, [FromBody] FeedbackRequestDto dto, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.SubmitFeedbackAsync(problemId, attemptId, dto.IsSolved, dto.Comments, authenticatedUserId, cancellationToken);
        var response = ApiResponse<DebuggingResultDto>.SuccessResult(result, "Your feedback has been submitted successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("{problemId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProblem(int problemId, CancellationToken cancellationToken)
    {
        var problem = await _problemService.GetProblemDetailsAsync(problemId, cancellationToken);
        if (problem == null)
        {
            throw AppError.NotFound($"Problem with ID {problemId} not found.");
        }

        var response = ApiResponse<Problem>.SuccessResult(problem, "Problem retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("code/{problemCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProblemByCode(string problemCode, CancellationToken cancellationToken)
    {
        var problem = await _problemService.GetProblemByCodeAsync(problemCode, cancellationToken);
        if (problem == null)
        {
            throw AppError.NotFound($"Problem with code {problemCode} not found.");
        }

        var response = ApiResponse<Problem>.SuccessResult(problem, "Problem retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("{problemId:int}/history")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProblemHistory(int problemId, CancellationToken cancellationToken)
    {
        // Non-owners get history without source/logs.
        var history = await _problemService.GetProblemHistoryAsync(problemId, TryGetAuthenticatedUserId(), cancellationToken);
        var response = ApiResponse<ProblemHistoryDto>.SuccessResult(history, "Problem history retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPlatformStats(CancellationToken cancellationToken)
    {
        var stats = await _problemService.GetPlatformStatsAsync(cancellationToken);
        var response = ApiResponse<PlatformStatsDto>.SuccessResult(stats, "Platform stats retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMyProblems([FromQuery] int recentCount = 8, CancellationToken cancellationToken = default)
    {
        var authenticatedUserId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.GetMyProblemsDashboardAsync(authenticatedUserId, recentCount, cancellationToken);
        var response = ApiResponse<MyProblemsDashboardDto>.SuccessResult(result, "Your problems were retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchProblems(
        [FromQuery] string? q,
        [FromQuery] string? technology,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _problemService.SearchProblemsAsync(q, technology, page, pageSize, cancellationToken);
        var response = ApiResponse<PagedResultDto<ProblemSearchResultDto>>.SuccessResult(result, "Results retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("articles")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchPublishedArticles(
        [FromQuery] string? q,
        [FromQuery] string? technology,
        [FromQuery] string[]? technologies,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        var tech = technology;
        if (technologies is { Length: > 0 })
        {
            var joined = string.Join(",", technologies.Where(t => !string.IsNullOrWhiteSpace(t)));
            tech = string.IsNullOrWhiteSpace(tech) ? joined : $"{tech},{joined}";
        }

        var result = await _problemService.SearchPublishedArticlesAsync(q, tech, page, pageSize, cancellationToken);
        var response = ApiResponse<PagedResultDto<KnowledgeArticleListItemDto>>.SuccessResult(result, "Published articles retrieved successfully.", GetTraceId());
        return Ok(response);
    }

    [HttpGet("article-technologies")]
    [AllowAnonymous]
    public async Task<IActionResult> ListArticleTechnologies(CancellationToken cancellationToken)
    {
        var result = await _problemService.ListPublishedArticleTechnologiesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<string>>.SuccessResult(result, "Article technologies loaded.", GetTraceId()));
    }

    [HttpGet("articles/saved")]
    [Authorize]
    public async Task<IActionResult> ListSavedArticles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.ListSavedArticlesAsync(userId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<KnowledgeArticleListItemDto>>.SuccessResult(result, "Saved articles loaded.", GetTraceId()));
    }

    [HttpGet("articles/{articleId:int}/engagement")]
    [AllowAnonymous]
    public async Task<IActionResult> GetArticleEngagement(int articleId, CancellationToken cancellationToken)
    {
        var viewerId = TryGetAuthenticatedUserId();
        var result = await _problemService.GetArticleEngagementAsync(articleId, viewerId, cancellationToken);
        return Ok(ApiResponse<ArticleEngagementDto>.SuccessResult(result, "Article engagement loaded.", GetTraceId()));
    }

    [HttpPost("articles/{articleId:int}/like")]
    [Authorize]
    public async Task<IActionResult> ToggleArticleLike(int articleId, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.ToggleArticleLikeAsync(articleId, userId, cancellationToken);
        return Ok(ApiResponse<ArticleToggleResultDto>.SuccessResult(result, result.Active ? "Article liked." : "Like removed.", GetTraceId()));
    }

    [HttpPost("articles/{articleId:int}/save")]
    [Authorize]
    public async Task<IActionResult> ToggleArticleSave(int articleId, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.ToggleArticleSaveAsync(articleId, userId, cancellationToken);
        return Ok(ApiResponse<ArticleToggleResultDto>.SuccessResult(result, result.Active ? "Article saved." : "Article unsaved.", GetTraceId()));
    }

    [HttpPost("articles/{articleId:int}/comments")]
    [Authorize]
    public async Task<IActionResult> AddArticleComment(
        int articleId,
        [FromBody] CreateArticleCommentDto dto,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _problemService.AddArticleCommentAsync(articleId, userId, dto?.Body ?? string.Empty, cancellationToken);
        return Ok(ApiResponse<ArticleCommentDto>.SuccessResult(result, "Comment added.", GetTraceId()));
    }

    [HttpDelete("articles/{articleId:int}/comments/{commentId:long}")]
    [Authorize]
    public async Task<IActionResult> DeleteArticleComment(int articleId, long commentId, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        await _problemService.DeleteArticleCommentAsync(articleId, commentId, userId, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Comment deleted.", GetTraceId()));
    }

    [HttpGet("articles/{idOrCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublishedArticle(string idOrCode, CancellationToken cancellationToken)
    {
        KnowledgeArticleDetailsDto? article = null;
        if (int.TryParse(idOrCode, out var numericId))
        {
            article = await _problemService.GetPublishedArticleAsync(knowledgeArticleId: numericId, cancellationToken: cancellationToken)
                ?? await _problemService.GetPublishedArticleAsync(problemId: numericId, cancellationToken: cancellationToken);
        }
        else
        {
            article = await _problemService.GetPublishedArticleAsync(articleOrProblemCode: idOrCode, cancellationToken: cancellationToken);
        }

        if (article == null)
        {
            throw AppError.NotFound($"Published article '{idOrCode}' was not found.");
        }

        var response = ApiResponse<KnowledgeArticleDetailsDto>.SuccessResult(article, "Article retrieved successfully.", GetTraceId());
        return Ok(response);
    }
}
