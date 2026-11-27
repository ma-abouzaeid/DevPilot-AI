using DevPilotAI.Application.Support.DTOs;

namespace DevPilotAI.Application.Common.Interfaces;

public interface ISupportTicketRealtime
{
    Task TicketUpdatedAsync(SupportTicketDetailsDto ticket, CancellationToken cancellationToken = default);
    Task MessageReceivedAsync(int supportTicketId, SupportTicketMessageDto message, CancellationToken cancellationToken = default);
    Task TicketResolvedAsync(int supportTicketId, SupportTicketDetailsDto ticket, CancellationToken cancellationToken = default);
    Task AdminNotificationAsync(int adminUserId, AdminNotificationDto notification, CancellationToken cancellationToken = default);
    Task UserNotificationAsync(int userId, AdminNotificationDto notification, CancellationToken cancellationToken = default);
}
