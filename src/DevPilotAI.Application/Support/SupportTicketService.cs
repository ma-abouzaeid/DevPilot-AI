using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Support.DTOs;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Support;

public class SupportTicketService : ISupportTicketService
{
    private readonly IApplicationDbContext _db;
    private readonly ISupportTicketRealtime _realtime;

    public SupportTicketService(IApplicationDbContext db, ISupportTicketRealtime realtime)
    {
        _db = db;
        _realtime = realtime;
    }

    public async Task<SupportTicketDetailsDto> CreateAsync(
        int userId,
        CreateSupportTicketDto dto,
        CancellationToken cancellationToken = default)
    {
        var title = (dto.Title ?? string.Empty).Trim();
        var description = (dto.Description ?? string.Empty).Trim();
        if (title.Length < 3 || description.Length < 10)
        {
            throw AppError.InvalidRequest("Title and description are required.", "INVALID_REQUEST");
        }

        var ticket = new SupportTicket
        {
            TicketCode = await GenerateTicketCodeAsync(cancellationToken),
            CreatedByUserId = userId,
            Title = title.Length > 200 ? title[..200] : title,
            Description = description.Length > 4000 ? description[..4000] : description,
            Status = SupportTicketStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync(cancellationToken);

        var creatorName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Developer";

        await NotifyAllAdminsAsync(
            ticket.SupportTicketId,
            "TICKET_OPENED",
            "New support ticket",
            $"{creatorName} opened {ticket.TicketCode}: {ticket.Title}",
            cancellationToken);

        return await MapDetailsAsync(ticket.SupportTicketId, userId, isAdmin: false, cancellationToken);
    }

    public async Task<IReadOnlyList<SupportTicketListItemDto>> ListMineAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.SupportTickets.AsNoTracking()
            .Where(t => t.CreatedByUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new SupportTicketListItemDto(
                t.SupportTicketId,
                t.TicketCode,
                t.Title,
                t.Status.ToString(),
                t.CreatedByUser.Name,
                t.AssignedAdminUser != null ? t.AssignedAdminUser.Name : null,
                t.CreatedAt,
                t.AcceptedAt,
                t.ResolvedAt,
                t.Messages.Count
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SupportTicketListItemDto>> ListForAdminAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SupportTickets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<SupportTicketStatus>(status.Trim(), ignoreCase: true, out var parsed))
        {
            query = query.Where(t => t.Status == parsed);
        }

        return await query
            .OrderByDescending(t => t.Status == SupportTicketStatus.Open)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new SupportTicketListItemDto(
                t.SupportTicketId,
                t.TicketCode,
                t.Title,
                t.Status.ToString(),
                t.CreatedByUser.Name,
                t.AssignedAdminUser != null ? t.AssignedAdminUser.Name : null,
                t.CreatedAt,
                t.AcceptedAt,
                t.ResolvedAt,
                t.Messages.Count
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupportTicketDetailsDto> GetAsync(
        int ticketId,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanViewAsync(ticketId, userId, isAdmin, cancellationToken);
        return await MapDetailsAsync(ticketId, userId, isAdmin, cancellationToken);
    }

    public async Task<SupportTicketDetailsDto> AcceptAsync(
        int ticketId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, cancellationToken)
            ?? throw AppError.NotFound("Support ticket was not found.");

        if (ticket.Status == SupportTicketStatus.Resolved)
        {
            throw AppError.Conflict("Resolved tickets cannot be accepted.", "TICKET_RESOLVED");
        }

        if (ticket.Status == SupportTicketStatus.Assigned && ticket.AssignedAdminUserId != adminUserId)
        {
            throw AppError.Conflict("This ticket is already assigned to another admin.", "TICKET_ASSIGNED");
        }

        if (ticket.Status == SupportTicketStatus.Open)
        {
            ticket.Status = SupportTicketStatus.Assigned;
            ticket.AssignedAdminUserId = adminUserId;
            ticket.AcceptedAt = DateTime.UtcNow;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await NotifyAllAdminsAsync(
                ticket.SupportTicketId,
                "TICKET_ACCEPTED",
                "Ticket accepted",
                $"{ticket.TicketCode} was accepted by an admin.",
                cancellationToken,
                excludeAdminUserId: adminUserId);
        }

        var details = await MapDetailsAsync(ticketId, adminUserId, isAdmin: true, cancellationToken);
        await _realtime.TicketUpdatedAsync(details, cancellationToken);
        return details;
    }

    public async Task<SupportTicketDetailsDto> ResolveAsync(
        int ticketId,
        int adminUserId,
        ResolveSupportTicketDto dto,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, cancellationToken)
            ?? throw AppError.NotFound("Support ticket was not found.");

        if (ticket.AssignedAdminUserId != adminUserId)
        {
            throw AppError.Forbidden("Only the assigned admin can resolve this ticket.");
        }

        if (ticket.Status == SupportTicketStatus.Resolved)
        {
            throw AppError.Conflict("Ticket is already resolved.", "TICKET_RESOLVED");
        }

        var resolution = (dto.ResolutionSummary ?? string.Empty).Trim();
        if (resolution.Length < 5)
        {
            throw AppError.InvalidRequest("Resolution summary is required.", "INVALID_REQUEST");
        }

        ticket.Status = SupportTicketStatus.Resolved;
        ticket.ResolutionSummary = resolution.Length > 4000 ? resolution[..4000] : resolution;
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        // Hard-delete chat history; keep ticket + resolution.
        var messages = await _db.SupportTicketMessages
            .Where(m => m.SupportTicketId == ticketId)
            .ToListAsync(cancellationToken);
        _db.SupportTicketMessages.RemoveRange(messages);

        await _db.SaveChangesAsync(cancellationToken);

        var details = await MapDetailsAsync(ticketId, adminUserId, isAdmin: true, cancellationToken);
        await _realtime.TicketResolvedAsync(ticketId, details, cancellationToken);
        return details;
    }

    public async Task<IReadOnlyList<SupportTicketMessageDto>> ListMessagesAsync(
        int ticketId,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessChatAsync(ticketId, userId, isAdmin, cancellationToken);

        return await _db.SupportTicketMessages.AsNoTracking()
            .Where(m => m.SupportTicketId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new SupportTicketMessageDto(
                m.SupportTicketMessageId,
                m.SupportTicketId,
                m.SenderUserId,
                m.SenderUser.Name,
                m.SenderUserId != m.SupportTicket.CreatedByUserId,
                m.Body,
                m.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<SupportTicketMessageDto> SendMessageAsync(
        int ticketId,
        int userId,
        bool isAdmin,
        string body,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessChatAsync(ticketId, userId, isAdmin, cancellationToken);

        var text = (body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw AppError.InvalidRequest("Message cannot be empty.", "INVALID_REQUEST");
        }

        if (text.Length > 4000) text = text[..4000];

        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, cancellationToken)
            ?? throw AppError.NotFound("Support ticket was not found.");

        if (ticket.Status == SupportTicketStatus.Resolved)
        {
            throw AppError.Conflict("Cannot chat on a resolved ticket.", "TICKET_RESOLVED");
        }

        var message = new SupportTicketMessage
        {
            SupportTicketId = ticketId,
            SenderUserId = userId,
            Body = text,
            CreatedAt = DateTime.UtcNow
        };
        _db.SupportTicketMessages.Add(message);
        ticket.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var senderName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "User";

        var dto = new SupportTicketMessageDto(
            message.SupportTicketMessageId,
            ticketId,
            userId,
            senderName,
            userId != ticket.CreatedByUserId,
            message.Body,
            message.CreatedAt);

        await _realtime.MessageReceivedAsync(ticketId, dto, cancellationToken);

        // Notify assigned admin when the user sends a message.
        if (userId == ticket.CreatedByUserId && ticket.AssignedAdminUserId.HasValue)
        {
            await NotifyAdminAsync(
                ticket.AssignedAdminUserId.Value,
                ticketId,
                "TICKET_MESSAGE",
                "New ticket message",
                $"{senderName} replied on {ticket.TicketCode}",
                cancellationToken);
        }

        return dto;
    }

    public async Task EnsureCanAccessChatAsync(
        int ticketId,
        int userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _db.SupportTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, cancellationToken)
            ?? throw AppError.NotFound("Support ticket was not found.");

        if (ticket.Status != SupportTicketStatus.Assigned)
        {
            throw AppError.Forbidden("Chat is available only after an admin accepts the ticket.");
        }

        var isOwner = ticket.CreatedByUserId == userId;
        var isAssignee = ticket.AssignedAdminUserId == userId;
        if (isOwner || isAssignee)
        {
            return;
        }

        // Other admins cannot join the chat room.
        throw AppError.Forbidden("Only the ticket owner and assigned admin can use this chat.");
    }

    public async Task<IReadOnlyList<AdminNotificationDto>> ListAdminNotificationsAsync(
        int adminUserId,
        int take = 30,
        CancellationToken cancellationToken = default)
    {
        if (take < 1 || take > 100) take = 30;
        return await _db.AdminNotifications.AsNoTracking()
            .Where(n => n.AdminUserId == adminUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new AdminNotificationDto(
                n.AdminNotificationId,
                n.SupportTicketId,
                n.Type,
                n.Title,
                n.Body,
                n.IsRead,
                n.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAdminNotificationsAsync(int adminUserId, CancellationToken cancellationToken = default)
    {
        return _db.AdminNotifications.CountAsync(
            n => n.AdminUserId == adminUserId && !n.IsRead,
            cancellationToken);
    }

    public async Task MarkAdminNotificationsReadAsync(
        int adminUserId,
        long? notificationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AdminNotifications.Where(n => n.AdminUserId == adminUserId && !n.IsRead);
        if (notificationId.HasValue)
        {
            query = query.Where(n => n.AdminNotificationId == notificationId.Value);
        }

        var items = await query.ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.IsRead = true;
        }

        if (items.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureCanViewAsync(int ticketId, int userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var ticket = await _db.SupportTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.SupportTicketId == ticketId, cancellationToken)
            ?? throw AppError.NotFound("Support ticket was not found.");

        if (ticket.CreatedByUserId == userId || isAdmin)
        {
            return;
        }

        throw AppError.Forbidden();
    }

    private async Task<SupportTicketDetailsDto> MapDetailsAsync(
        int ticketId,
        int viewerUserId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var t = await _db.SupportTickets.AsNoTracking()
            .Where(x => x.SupportTicketId == ticketId)
            .Select(x => new
            {
                x.SupportTicketId,
                x.TicketCode,
                x.Title,
                x.Description,
                Status = x.Status.ToString(),
                x.CreatedByUserId,
                CreatedByName = x.CreatedByUser.Name,
                x.AssignedAdminUserId,
                AssignedAdminName = x.AssignedAdminUser != null ? x.AssignedAdminUser.Name : null,
                x.ResolutionSummary,
                x.CreatedAt,
                x.AcceptedAt,
                x.ResolvedAt,
                StatusEnum = x.Status
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppError.NotFound("Support ticket was not found.");

        var isOwner = t.CreatedByUserId == viewerUserId;
        var isAssignee = t.AssignedAdminUserId == viewerUserId;
        var canChat = t.StatusEnum == SupportTicketStatus.Assigned && (isOwner || isAssignee);
        var canAccept = isAdmin && t.StatusEnum == SupportTicketStatus.Open;
        var canResolve = isAdmin && isAssignee && t.StatusEnum == SupportTicketStatus.Assigned;

        return new SupportTicketDetailsDto(
            t.SupportTicketId,
            t.TicketCode,
            t.Title,
            t.Description,
            t.Status,
            t.CreatedByUserId,
            t.CreatedByName,
            t.AssignedAdminUserId,
            t.AssignedAdminName,
            t.ResolutionSummary,
            t.CreatedAt,
            t.AcceptedAt,
            t.ResolvedAt,
            canChat,
            canAccept,
            canResolve
        );
    }

    private async Task NotifyAllAdminsAsync(
        int ticketId,
        string type,
        string title,
        string body,
        CancellationToken cancellationToken,
        int? excludeAdminUserId = null)
    {
        var adminIds = await _db.UserRoles.AsNoTracking()
            .Where(ur => ur.Role.Name == AppRoles.Admin)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var adminId in adminIds)
        {
            if (excludeAdminUserId.HasValue && adminId == excludeAdminUserId.Value) continue;
            await NotifyAdminAsync(adminId, ticketId, type, title, body, cancellationToken);
        }
    }

    private async Task NotifyAdminAsync(
        int adminUserId,
        int ticketId,
        string type,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var notification = new AdminNotification
        {
            AdminUserId = adminUserId,
            SupportTicketId = ticketId,
            Type = type,
            Title = title.Length > 200 ? title[..200] : title,
            Body = body.Length > 1000 ? body[..1000] : body,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.AdminNotifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        await _realtime.AdminNotificationAsync(
            adminUserId,
            new AdminNotificationDto(
                notification.AdminNotificationId,
                ticketId,
                notification.Type,
                notification.Title,
                notification.Body,
                false,
                notification.CreatedAt),
            cancellationToken);
    }

    private async Task<string> GenerateTicketCodeAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 8; i++)
        {
            var code = "ST-" + DateTime.UtcNow.ToString("yyMMdd") + "-" + Random.Shared.Next(1000, 9999);
            var exists = await _db.SupportTickets.AnyAsync(t => t.TicketCode == code, cancellationToken);
            if (!exists) return code;
        }

        return "ST-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
    }
}
