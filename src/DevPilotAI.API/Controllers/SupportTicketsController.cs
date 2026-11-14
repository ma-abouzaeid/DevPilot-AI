using System.Security.Claims;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Support;
using DevPilotAI.Application.Support.DTOs;
using DevPilotAI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/support-tickets")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.General)]
public class SupportTicketsController : ControllerBase
{
    private readonly ISupportTicketService _tickets;
    private readonly IApplicationDbContext _db;

    public SupportTicketsController(ISupportTicketService tickets, IApplicationDbContext db)
    {
        _tickets = tickets;
        _db = db;
    }

    private string GetTraceId() =>
        HttpContext?.Request?.Headers.TryGetValue("X-Correlation-ID", out var correlationId) == true && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    private int GetRequiredUserId()
    {
        var raw =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("nameid");
        if (!int.TryParse(raw, out var userId))
        {
            throw AppError.Unauthorized("Authentication is required.");
        }

        return userId;
    }

    private static bool HasAdminClaim(ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.Admin) ||
        user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role ||
             c.Type.Equals("role", StringComparison.OrdinalIgnoreCase) ||
             c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase) ||
             c.Type.EndsWith("/claims/role", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(c.Value, AppRoles.Admin, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Matches AdminPolicy: JWT Admin claim OR Admin row in UserRoles.
    /// </summary>
    private async Task<bool> IsAdminAsync(CancellationToken cancellationToken)
    {
        if (HasAdminClaim(User)) return true;

        var userId = GetRequiredUserId();
        return await _db.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == AppRoles.Admin, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupportTicketDto dto, CancellationToken cancellationToken)
    {
        var result = await _tickets.CreateAsync(GetRequiredUserId(), dto, cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailsDto>.SuccessResult(result, "Support ticket created.", GetTraceId()));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken)
    {
        var result = await _tickets.ListMineAsync(GetRequiredUserId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SupportTicketListItemDto>>.SuccessResult(result, "Your tickets loaded.", GetTraceId()));
    }

    [HttpGet("{ticketId:int}")]
    public async Task<IActionResult> Get(int ticketId, CancellationToken cancellationToken)
    {
        var result = await _tickets.GetAsync(ticketId, GetRequiredUserId(), await IsAdminAsync(cancellationToken), cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailsDto>.SuccessResult(result, "Ticket loaded.", GetTraceId()));
    }

    [HttpGet("{ticketId:int}/messages")]
    public async Task<IActionResult> ListMessages(int ticketId, CancellationToken cancellationToken)
    {
        var result = await _tickets.ListMessagesAsync(ticketId, GetRequiredUserId(), await IsAdminAsync(cancellationToken), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SupportTicketMessageDto>>.SuccessResult(result, "Messages loaded.", GetTraceId()));
    }

    [HttpPost("{ticketId:int}/messages")]
    public async Task<IActionResult> SendMessage(int ticketId, [FromBody] SendSupportMessageDto dto, CancellationToken cancellationToken)
    {
        var result = await _tickets.SendMessageAsync(
            ticketId,
            GetRequiredUserId(),
            await IsAdminAsync(cancellationToken),
            dto?.Body ?? string.Empty,
            cancellationToken);
        return Ok(ApiResponse<SupportTicketMessageDto>.SuccessResult(result, "Message sent.", GetTraceId()));
    }

    [HttpGet]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> ListForAdmin([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var result = await _tickets.ListForAdminAsync(status, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<SupportTicketListItemDto>>.SuccessResult(result, "Tickets loaded.", GetTraceId()));
    }

    [HttpPost("{ticketId:int}/accept")]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> Accept(int ticketId, CancellationToken cancellationToken)
    {
        var result = await _tickets.AcceptAsync(ticketId, GetRequiredUserId(), cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailsDto>.SuccessResult(result, "Ticket accepted.", GetTraceId()));
    }

    [HttpPost("{ticketId:int}/resolve")]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> Resolve(int ticketId, [FromBody] ResolveSupportTicketDto dto, CancellationToken cancellationToken)
    {
        var result = await _tickets.ResolveAsync(ticketId, GetRequiredUserId(), dto, cancellationToken);
        return Ok(ApiResponse<SupportTicketDetailsDto>.SuccessResult(result, "Ticket resolved. Chat history deleted.", GetTraceId()));
    }

    [HttpGet("admin/notifications")]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        var items = await _tickets.ListAdminNotificationsAsync(userId, 40, cancellationToken);
        var unread = await _tickets.CountUnreadAdminNotificationsAsync(userId, cancellationToken);
        return Ok(ApiResponse<object>.SuccessResult(new { items, unread }, "Notifications loaded.", GetTraceId()));
    }

    [HttpPost("admin/notifications/read")]
    [Authorize(Policy = AppRoles.AdminPolicy)]
    public async Task<IActionResult> MarkNotificationsRead([FromQuery] long? id, CancellationToken cancellationToken)
    {
        await _tickets.MarkAdminNotificationsReadAsync(GetRequiredUserId(), id, cancellationToken);
        return Ok(ApiResponse<object?>.SuccessResult(null, "Notifications marked read.", GetTraceId()));
    }
}
