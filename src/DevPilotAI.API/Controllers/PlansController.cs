using System.Security.Claims;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Plans;
using DevPilotAI.Application.Plans.DTOs;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.General)]
public class PlansController : ControllerBase
{
    private readonly IPlanUsageService _planUsageService;
    private readonly ICreditCostService _creditCostService;

    public PlansController(IPlanUsageService planUsageService, ICreditCostService creditCostService)
    {
        _planUsageService = planUsageService;
        _creditCostService = creditCostService;
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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var plans = await _planUsageService.ListPublicPlansAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PlanListItemDto>>.SuccessResult(plans, "Plans retrieved.", GetTraceId()));
    }

    [HttpGet("credit-costs")]
    [AllowAnonymous]
    public async Task<IActionResult> CreditCosts(CancellationToken cancellationToken)
    {
        var costs = await _creditCostService.ListPublicAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CreditCostItemDto>>.SuccessResult(costs, "Credit costs retrieved.", GetTraceId()));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> MyUsage(CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var usage = await _planUsageService.GetMyUsageAsync(userId, cancellationToken);
        return Ok(ApiResponse<MyPlanUsageDto>.SuccessResult(usage, "Plan usage retrieved.", GetTraceId()));
    }

    /// <summary>
    /// Self-serve plan activation (no Stripe yet). Use for Pro upgrade / Free downgrade demos.
    /// </summary>
    [HttpPost("activate")]
    [Authorize]
    public async Task<IActionResult> Activate([FromBody] ActivatePlanRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = GetRequiredAuthenticatedUserId();
        var usage = await _planUsageService.ActivatePlanAsync(userId, dto.PlanCode, cancellationToken);
        return Ok(ApiResponse<MyPlanUsageDto>.SuccessResult(usage, $"Plan {usage.PlanCode} activated.", GetTraceId()));
    }
}
