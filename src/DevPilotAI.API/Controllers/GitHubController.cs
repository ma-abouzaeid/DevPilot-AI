using System.Security.Claims;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.GitHub;
using DevPilotAI.Application.GitHub.DTOs;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.General)]
public class GitHubController : ControllerBase
{
    private readonly IGitHubService _gitHubService;

    public GitHubController(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
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

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _gitHubService.GetConnectionStatusAsync(userId, cancellationToken);
        return Ok(ApiResponse<GitHubConnectionStatusDto>.SuccessResult(result, "GitHub connection status retrieved.", GetTraceId()));
    }

    [HttpGet("connect")]
    [Authorize]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _gitHubService.GetAuthorizeUrlAsync(userId, cancellationToken);
        return Ok(ApiResponse<GitHubAuthorizeUrlDto>.SuccessResult(result, "Authorize URL created.", GetTraceId()));
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return BadRequest(ApiResponse<object?>.FailureResult($"GitHub authorization failed: {error}", "GITHUB_OAUTH_DENIED", null, GetTraceId()));
        }

        var redirectUrl = await _gitHubService.HandleOAuthCallbackAsync(code ?? string.Empty, state ?? string.Empty, cancellationToken);
        return Redirect(redirectUrl);
    }

    [HttpDelete("disconnect")]
    [Authorize]
    public async Task<IActionResult> Disconnect(CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        await _gitHubService.DisconnectAsync(userId, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "GitHub disconnected.", GetTraceId()));
    }

    [HttpGet("repos")]
    [Authorize]
    public async Task<IActionResult> ListRepos(CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _gitHubService.ListRepositoriesAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GitHubRepoDto>>.SuccessResult(result, "Repositories retrieved.", GetTraceId()));
    }

    [HttpGet("repos/{owner}/{repo}/tree")]
    [Authorize]
    public async Task<IActionResult> GetTree(string owner, string repo, [FromQuery] string? branch, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _gitHubService.GetRepositoryTreeAsync(userId, $"{owner}/{repo}", branch, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<GitHubTreeItemDto>>.SuccessResult(result, "Repository tree retrieved.", GetTraceId()));
    }

    [HttpGet("repos/{owner}/{repo}/contents")]
    [Authorize]
    public async Task<IActionResult> GetFile(string owner, string repo, [FromQuery] string path, [FromQuery] string? branch, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _gitHubService.GetFileAsync(userId, $"{owner}/{repo}", path, branch, cancellationToken);
        return Ok(ApiResponse<GitHubFileDto>.SuccessResult(result, "File content retrieved.", GetTraceId()));
    }

    [HttpPost("review")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiAnalysis)]
    public async Task<IActionResult> Review([FromBody] GitHubCodeReviewRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _gitHubService.ReviewAsync(userId, dto, cancellationToken);
        return Ok(ApiResponse<GitHubCodeReviewResultDto>.SuccessResult(result, "Code review completed.", GetTraceId()));
    }

    [HttpPost("debug")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.AiAnalysis)]
    public async Task<IActionResult> DebugFromFile([FromBody] GitHubDebugRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var result = await _gitHubService.DebugFromFileAsync(userId, dto, cancellationToken);
        return Ok(ApiResponse<DebuggingResultDto>.SuccessResult(result, "GitHub file debugging completed.", GetTraceId()));
    }
}
