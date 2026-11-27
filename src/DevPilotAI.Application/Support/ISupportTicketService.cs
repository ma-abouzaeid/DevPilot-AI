using DevPilotAI.Application.Support.DTOs;

namespace DevPilotAI.Application.Support;

public interface ISupportTicketService
{
    Task<SupportTicketDetailsDto> CreateAsync(int userId, CreateSupportTicketDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupportTicketListItemDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupportTicketListItemDto>> ListForAdminAsync(string? status, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailsDto> GetAsync(int ticketId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailsDto> AcceptAsync(int ticketId, int adminUserId, CancellationToken cancellationToken = default);
    Task<SupportTicketDetailsDto> ResolveAsync(int ticketId, int adminUserId, ResolveSupportTicketDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupportTicketMessageDto>> ListMessagesAsync(int ticketId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<SupportTicketMessageDto> SendMessageAsync(
        int ticketId,
        int userId,
        bool isAdmin,
        string body,
        CancellationToken cancellationToken = default,
        bool asAdmin = false);
    Task EnsureCanAccessChatAsync(int ticketId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminNotificationDto>> ListAdminNotificationsAsync(
        int adminUserId,
        int take = 30,
        CancellationToken cancellationToken = default,
        bool backfillOpenTickets = true);
    Task<int> CountUnreadAdminNotificationsAsync(int adminUserId, CancellationToken cancellationToken = default);
    Task MarkAdminNotificationsReadAsync(
        int adminUserId,
        long? notificationId = null,
        int? supportTicketId = null,
        CancellationToken cancellationToken = default);
}
