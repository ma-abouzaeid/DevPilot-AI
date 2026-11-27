namespace DevPilotAI.Application.Common.Interfaces;

/// <summary>
/// Tracks which users currently have a support ticket chat open (SignalR JoinTicket).
/// Used to suppress bell notifications while the conversation is visible.
/// </summary>
public interface ISupportTicketPresence
{
    void JoinTicket(int userId, int ticketId, string connectionId);
    void LeaveTicket(int userId, int ticketId, string connectionId);
    void RemoveConnection(string connectionId);
    bool IsViewingTicket(int userId, int ticketId);
}
