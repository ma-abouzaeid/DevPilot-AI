using DevPilotAI.API.Hubs;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Support.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace DevPilotAI.API.Services;

public class SupportTicketRealtime : ISupportTicketRealtime
{
    private readonly IHubContext<SupportTicketHub> _hub;

    public SupportTicketRealtime(IHubContext<SupportTicketHub> hub)
    {
        _hub = hub;
    }

    public Task TicketUpdatedAsync(SupportTicketDetailsDto ticket, CancellationToken cancellationToken = default)
    {
        return _hub.Clients.Group(SupportTicketHub.TicketGroup(ticket.SupportTicketId))
            .SendAsync("ticketUpdated", ticket, cancellationToken);
    }

    public Task MessageReceivedAsync(int supportTicketId, SupportTicketMessageDto message, CancellationToken cancellationToken = default)
    {
        return _hub.Clients.Group(SupportTicketHub.TicketGroup(supportTicketId))
            .SendAsync("messageReceived", message, cancellationToken);
    }

    public Task TicketResolvedAsync(int supportTicketId, SupportTicketDetailsDto ticket, CancellationToken cancellationToken = default)
    {
        return _hub.Clients.Group(SupportTicketHub.TicketGroup(supportTicketId))
            .SendAsync("ticketResolved", ticket, cancellationToken);
    }

    public Task AdminNotificationAsync(int adminUserId, AdminNotificationDto notification, CancellationToken cancellationToken = default)
    {
        return _hub.Clients.Group(SupportTicketHub.AdminGroup(adminUserId))
            .SendAsync("adminNotification", notification, cancellationToken);
    }

    public Task UserNotificationAsync(int userId, AdminNotificationDto notification, CancellationToken cancellationToken = default)
    {
        return _hub.Clients.Group(SupportTicketHub.UserGroup(userId))
            .SendAsync("userNotification", notification, cancellationToken);
    }
}
