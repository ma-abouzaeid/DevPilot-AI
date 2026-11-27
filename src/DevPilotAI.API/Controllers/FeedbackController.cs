using System.Security.Claims;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Feedback;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.General)]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedback;

    public FeedbackController(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private int GetRequiredUserId()
    {
        var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(claim, out var id) || id <= 0)
        {
            throw AppError.Unauthorized("User identity claim is missing or invalid.");
        }

        return id;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFeedbackRequestDto dto, CancellationToken cancellationToken)
    {
        var item = await _feedback.CreateAsync(GetRequiredUserId(), dto, "Web", cancellationToken);
        return Ok(ApiResponse<FeedbackListItemDto>.SuccessResult(item, "Feedback submitted.", GetTraceId()));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var items = await _feedback.ListMineAsync(GetRequiredUserId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeedbackListItemDto>>.SuccessResult(items, "Feedback loaded.", GetTraceId()));
    }

    [HttpGet]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var items = await _feedback.ListAllAsync(status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FeedbackListItemDto>>.SuccessResult(items, "Feedback loaded.", GetTraceId()));
    }

    [HttpPost("{id:long}/reviewed")]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> MarkReviewed(long id, CancellationToken cancellationToken)
    {
        await _feedback.MarkReviewedAsync(id, GetRequiredUserId(), cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Marked reviewed.", GetTraceId()));
    }
}
