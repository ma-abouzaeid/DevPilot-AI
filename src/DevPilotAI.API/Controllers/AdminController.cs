using System.Security.Claims;
using DevPilotAI.Application.Admin;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Plans;
using DevPilotAI.Application.Plans.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.API.Controllers;

[Authorize(Policy = AppRoles.AdminPolicy)]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ICreditCostService _creditCostService;

    public AdminController(IAdminService adminService, ICreditCostService creditCostService)
    {
        _adminService = adminService;
        _creditCostService = creditCostService;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private int GetRequiredUserId()
    {
        var userIdClaim =
            User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            User?.FindFirst("sub")?.Value ??
            User?.FindFirst("nameid")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId) || userId <= 0)
        {
            throw AppError.Unauthorized("Invalid or missing user identity in access token.");
        }
        return userId;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        var data = await _adminService.GetOverviewAsync(cancellationToken);
        return Ok(ApiResponse<AdminOverviewDto>.SuccessResult(data, "Admin overview loaded.", GetTraceId()));
    }

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? search,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.ListUsersAsync(search, includeDeleted, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<AdminUserListItemDto>>.SuccessResult(data, "Users loaded.", GetTraceId()));
    }

    [HttpGet("users/{userId:int}")]
    public async Task<IActionResult> GetUser(int userId, CancellationToken cancellationToken)
    {
        var data = await _adminService.GetUserAsync(userId, cancellationToken);
        return Ok(ApiResponse<AdminUserDetailDto>.SuccessResult(data, "User loaded.", GetTraceId()));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminUserRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _adminService.CreateUserAsync(GetRequiredUserId(), request, cancellationToken);
        return Ok(ApiResponse<AdminUserDetailDto>.SuccessResult(data, "User created.", GetTraceId()));
    }

    [HttpPut("users/{userId:int}")]
    public async Task<IActionResult> UpdateUser(int userId, [FromBody] UpdateAdminUserRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _adminService.UpdateUserAsync(GetRequiredUserId(), userId, request, cancellationToken);
        return Ok(ApiResponse<AdminUserDetailDto>.SuccessResult(data, "User updated.", GetTraceId()));
    }

    [HttpPost("users/{userId:int}/deactivate")]
    public async Task<IActionResult> DeactivateUser(int userId, CancellationToken cancellationToken)
    {
        await _adminService.DeactivateUserAsync(GetRequiredUserId(), userId, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "User deactivated.", GetTraceId()));
    }

    [HttpPost("users/{userId:int}/restore")]
    public async Task<IActionResult> RestoreUser(int userId, CancellationToken cancellationToken)
    {
        await _adminService.RestoreUserAsync(GetRequiredUserId(), userId, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "User restored.", GetTraceId()));
    }

    [HttpPost("users/{userId:int}/plan")]
    public async Task<IActionResult> AssignPlan(int userId, [FromBody] AssignUserPlanRequestDto request, CancellationToken cancellationToken)
    {
        await _adminService.AssignUserPlanAsync(GetRequiredUserId(), userId, request, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Plan assigned.", GetTraceId()));
    }

    [HttpPost("users/{userId:int}/admin-role")]
    public async Task<IActionResult> SetAdminRole(int userId, [FromBody] SetUserAdminRequestDto request, CancellationToken cancellationToken)
    {
        await _adminService.SetUserAdminAsync(GetRequiredUserId(), userId, request, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, request.IsAdmin ? "Admin role granted." : "Admin role revoked.", GetTraceId()));
    }

    [HttpPost("users/{userId:int}/unlimited-credits")]
    public async Task<IActionResult> SetUnlimitedCredits(int userId, [FromBody] SetUserUnlimitedCreditsRequestDto request, CancellationToken cancellationToken)
    {
        await _adminService.SetUserUnlimitedCreditsAsync(GetRequiredUserId(), userId, request, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(
            null,
            request.HasUnlimitedCredits ? "Unlimited credits granted." : "Unlimited credits revoked.",
            GetTraceId()));
    }

    [HttpGet("plans")]
    public async Task<IActionResult> ListPlans(CancellationToken cancellationToken)
    {
        var data = await _adminService.ListPlansAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminPlanDto>>.SuccessResult(data, "Plans loaded.", GetTraceId()));
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CreateAdminPlanRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _adminService.CreatePlanAsync(GetRequiredUserId(), request, cancellationToken);
        return Ok(ApiResponse<AdminPlanDto>.SuccessResult(data, "Plan created.", GetTraceId()));
    }

    [HttpPut("plans/{planId:int}")]
    public async Task<IActionResult> UpdatePlan(int planId, [FromBody] UpdateAdminPlanRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _adminService.UpdatePlanAsync(GetRequiredUserId(), planId, request, cancellationToken);
        return Ok(ApiResponse<AdminPlanDto>.SuccessResult(data, "Plan updated.", GetTraceId()));
    }

    [HttpGet("credit-costs")]
    public async Task<IActionResult> ListCreditCosts(CancellationToken cancellationToken)
    {
        var data = await _creditCostService.ListAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CreditCostItemDto>>.SuccessResult(data, "Credit costs loaded.", GetTraceId()));
    }

    [HttpPut("credit-costs")]
    public async Task<IActionResult> UpdateCreditCosts([FromBody] UpdateCreditCostsRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _creditCostService.UpdateAsync(request.Items, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CreditCostItemDto>>.SuccessResult(data, "Credit costs updated.", GetTraceId()));
    }

    [HttpGet("articles")]
    public async Task<IActionResult> ListArticles(
        [FromQuery] string? search,
        [FromQuery] bool? isPublished,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.ListArticlesAsync(search, isPublished, includeDeleted, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<AdminArticleListItemDto>>.SuccessResult(data, "Articles loaded.", GetTraceId()));
    }

    [HttpGet("articles/{articleId:int}")]
    public async Task<IActionResult> GetArticle(int articleId, CancellationToken cancellationToken)
    {
        var data = await _adminService.GetArticleAsync(articleId, cancellationToken);
        return Ok(ApiResponse<AdminArticleDetailDto>.SuccessResult(data, "Article loaded.", GetTraceId()));
    }

    [HttpPut("articles/{articleId:int}")]
    public async Task<IActionResult> UpdateArticle(int articleId, [FromBody] UpdateAdminArticleRequestDto request, CancellationToken cancellationToken)
    {
        var data = await _adminService.UpdateArticleAsync(GetRequiredUserId(), articleId, request, cancellationToken);
        return Ok(ApiResponse<AdminArticleDetailDto>.SuccessResult(data, "Article updated.", GetTraceId()));
    }

    [HttpPost("articles/{articleId:int}/publish")]
    public async Task<IActionResult> PublishArticle(int articleId, CancellationToken cancellationToken)
    {
        await _adminService.SetArticlePublishedAsync(GetRequiredUserId(), articleId, true, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Article published.", GetTraceId()));
    }

    [HttpPost("articles/{articleId:int}/unpublish")]
    public async Task<IActionResult> UnpublishArticle(int articleId, CancellationToken cancellationToken)
    {
        await _adminService.SetArticlePublishedAsync(GetRequiredUserId(), articleId, false, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Article unpublished.", GetTraceId()));
    }

    [HttpDelete("articles/{articleId:int}")]
    public async Task<IActionResult> DeleteArticle(int articleId, CancellationToken cancellationToken)
    {
        await _adminService.SoftDeleteArticleAsync(GetRequiredUserId(), articleId, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Article soft-deleted.", GetTraceId()));
    }

    [HttpPost("articles/{articleId:int}/restore")]
    public async Task<IActionResult> RestoreArticle(int articleId, CancellationToken cancellationToken)
    {
        await _adminService.RestoreArticleAsync(GetRequiredUserId(), articleId, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Article restored.", GetTraceId()));
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> ListAuditLogs(
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.ListAuditLogsAsync(userId, action, fromUtc, toUtc, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<AdminAuditLogDto>>.SuccessResult(data, "Audit logs loaded.", GetTraceId()));
    }

    [HttpGet("ai-usage/capacity")]
    public async Task<IActionResult> AiUsageCapacity(CancellationToken cancellationToken = default)
    {
        var data = await _adminService.GetAiUsageCapacityAsync(cancellationToken);
        return Ok(ApiResponse<AdminAiUsageCapacityDto>.SuccessResult(data, "AI usage capacity loaded.", GetTraceId()));
    }

    [HttpGet("ai-usage")]
    public async Task<IActionResult> ListAiUsage(
        [FromQuery] int? userId,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.ListAiUsageAsync(userId, status, fromUtc, toUtc, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<AdminAiUsageDto>>.SuccessResult(data, "AI usage loaded.", GetTraceId()));
    }

    [HttpGet("ai-usage/export")]
    public async Task<IActionResult> ExportAiUsage(
        [FromQuery] int? userId,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _adminService.ExportAiUsageExcelAsync(userId, status, fromUtc, toUtc, cancellationToken);
        var fileName = $"ai-usage-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("audit-logs/export")]
    public async Task<IActionResult> ExportAuditLogs(
        [FromQuery] int? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _adminService.ExportAuditLogsExcelAsync(userId, action, fromUtc, toUtc, cancellationToken);
        var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("system-errors")]
    public async Task<IActionResult> ListSystemErrors(
        [FromQuery] int? userId,
        [FromQuery] string? pathContains,
        [FromQuery] string? errorCode,
        [FromQuery] int? minStatusCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var data = await _adminService.ListSystemErrorsAsync(
            userId, pathContains, errorCode, minStatusCode, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResultDto<AdminSystemErrorDto>>.SuccessResult(data, "System errors loaded.", GetTraceId()));
    }

    [HttpGet("system-errors/export")]
    public async Task<IActionResult> ExportSystemErrors(
        [FromQuery] int? userId,
        [FromQuery] string? pathContains,
        [FromQuery] string? errorCode,
        [FromQuery] int? minStatusCode,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _adminService.ExportSystemErrorsExcelAsync(
            userId, pathContains, errorCode, minStatusCode, cancellationToken);
        var fileName = $"system-errors-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
