using System.Security.Claims;
using DevPilotAI.Application.Challenges;
using DevPilotAI.Application.Challenges.DTOs;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.General)]
public class ChallengesController : ControllerBase
{
    private readonly IChallengeService _challengeService;

    public ChallengesController(IChallengeService challengeService)
    {
        _challengeService = challengeService;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private int? TryGetUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) && id > 0 ? id : null;
    }

    private int GetRequiredAuthenticatedUserId()
    {
        return TryGetUserId() ?? throw AppError.Unauthorized("User identity claim is missing or invalid.");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var items = await _challengeService.ListPublishedAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ChallengeListItemDto>>.SuccessResult(items, "Challenges retrieved.", GetTraceId()));
    }

    [HttpGet("leaderboard")]
    [Authorize]
    public async Task<IActionResult> Leaderboard([FromQuery] int top = 10, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _challengeService.GetLeaderboardAsync(userId, top, cancellationToken);
        return Ok(ApiResponse<MyChallengeStatsDto>.SuccessResult(result, "Leaderboard retrieved.", GetTraceId()));
    }

    [HttpGet("{challengeId:int}")]
    [Authorize]
    public async Task<IActionResult> GetById(int challengeId, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var item = await _challengeService.GetByIdAsync(challengeId, userId, cancellationToken);
        if (item == null)
        {
            throw AppError.NotFound($"Challenge {challengeId} was not found.");
        }

        return Ok(ApiResponse<ChallengeDetailsDto>.SuccessResult(item, "Challenge retrieved.", GetTraceId()));
    }

    [HttpPost("{challengeId:int}/submit")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiAnalysis)]
    public async Task<IActionResult> Submit(int challengeId, [FromBody] SubmitChallengeSolutionDto dto, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _challengeService.SubmitSolutionAsync(challengeId, userId, dto, cancellationToken);
        return Ok(ApiResponse<ChallengeEvaluationResultDto>.SuccessResult(result, "Challenge solution evaluated.", GetTraceId()));
    }
}
