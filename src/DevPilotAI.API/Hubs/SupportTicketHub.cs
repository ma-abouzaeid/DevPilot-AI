using System.Security.Claims;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevPilotAI.API.Hubs;

[Authorize]
public class SupportTicketHub : Hub
{
    private readonly ISupportTicketService _tickets;

    public SupportTicketHub(ISupportTicketService tickets)
    {
        _tickets = tickets;
    }

    public static string TicketGroup(int ticketId) => "ticket-" + ticketId;
    public static string AdminGroup(int adminUserId) => "admin-" + adminUserId;

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue && IsAdmin(Context.User))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup(userId.Value));
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinTicket(int ticketId)
    {
        var userId = GetRequiredUserId();
        var isAdmin = IsAdmin(Context.User);
        await _tickets.EnsureCanAccessChatAsync(ticketId, userId, isAdmin, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, TicketGroup(ticketId));
    }

    public async Task LeaveTicket(int ticketId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TicketGroup(ticketId));
    }

    public async Task SendMessage(int ticketId, string body)
    {
        var userId = GetRequiredUserId();
        var isAdmin = IsAdmin(Context.User);
        await _tickets.SendMessageAsync(ticketId, userId, isAdmin, body, Context.ConnectionAborted);
    }

    private int GetRequiredUserId()
    {
        return GetUserId() ?? throw new HubException("Authentication is required.");
    }

    private int? GetUserId()
    {
        var raw =
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            Context.User?.FindFirstValue("sub") ??
            Context.User?.FindFirstValue("nameid");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static bool IsAdmin(ClaimsPrincipal? user)
    {
        if (user == null) return false;
        if (user.IsInRole(AppRoles.Admin)) return true;
        return user.Claims.Any(c =>
            (c.Type == ClaimTypes.Role || c.Type.Equals("role", StringComparison.OrdinalIgnoreCase) ||
             c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase) ||
             c.Type.EndsWith("/claims/role", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(c.Value, AppRoles.Admin, StringComparison.OrdinalIgnoreCase));
    }
}
