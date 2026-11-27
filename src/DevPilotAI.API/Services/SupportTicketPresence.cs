using System.Collections.Concurrent;
using DevPilotAI.Application.Common.Interfaces;

namespace DevPilotAI.API.Services;

public sealed class SupportTicketPresence : ISupportTicketPresence
{
    // connectionId -> (userId, ticketId)
    private readonly ConcurrentDictionary<string, (int UserId, int TicketId)> _byConnection = new();
    // "userId:ticketId" -> connection ids currently viewing that ticket
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _viewers = new();

    private static string ViewerKey(int userId, int ticketId) => userId + ":" + ticketId;

    public void JoinTicket(int userId, int ticketId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || userId < 1 || ticketId < 1) return;

        // One connection views at most one ticket.
        RemoveConnection(connectionId);

        _byConnection[connectionId] = (userId, ticketId);
        var set = _viewers.GetOrAdd(ViewerKey(userId, ticketId), _ => new ConcurrentDictionary<string, byte>());
        set[connectionId] = 0;
    }

    public void LeaveTicket(int userId, int ticketId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;

        if (_byConnection.TryGetValue(connectionId, out var current) &&
            current.UserId == userId &&
            current.TicketId == ticketId)
        {
            RemoveConnection(connectionId);
            return;
        }

        if (_viewers.TryGetValue(ViewerKey(userId, ticketId), out var set))
        {
            set.TryRemove(connectionId, out _);
            if (set.IsEmpty)
                _viewers.TryRemove(ViewerKey(userId, ticketId), out _);
        }
    }

    public void RemoveConnection(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return;
        if (!_byConnection.TryRemove(connectionId, out var viewing)) return;

        var key = ViewerKey(viewing.UserId, viewing.TicketId);
        if (_viewers.TryGetValue(key, out var set))
        {
            set.TryRemove(connectionId, out _);
            if (set.IsEmpty)
                _viewers.TryRemove(key, out _);
        }
    }

    public bool IsViewingTicket(int userId, int ticketId)
    {
        if (userId < 1 || ticketId < 1) return false;
        return _viewers.TryGetValue(ViewerKey(userId, ticketId), out var set) && !set.IsEmpty;
    }
}
