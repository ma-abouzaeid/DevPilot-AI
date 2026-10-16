using System.Security.Claims;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Services;
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

    private int GetRequiredAuthenticatedUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var id) || id <= 0)
        {
            throw AppError.Unauthorized("User identity claim is missing or invalid.");
        }
        return id;
    }

    [HttpPost]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiAnalysis)]
    public async Task<IActionResult> SubmitProblem([FromBody] CreateProblemDto dto, CancellationToken cancellationToken)
    {
        var authenticatedUserId = GetRequiredAuthenticatedUserId();
        dto = dto with { UserId = authenticatedUserId };

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
        var history = await _problemService.GetProblemHistoryAsync(problemId, cancellationToken);
        var response = ApiResponse<ProblemHistoryDto>.SuccessResult(history, "Problem history retrieved successfully.", GetTraceId());
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
}
