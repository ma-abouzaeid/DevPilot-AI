using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Support.DTOs;

public class CreateSupportTicketDto
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;
}

public class ResolveSupportTicketDto
{
    [Required]
    [StringLength(4000, MinimumLength = 5)]
    public string ResolutionSummary { get; set; } = string.Empty;
}

public class SendSupportMessageDto
{
    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Body { get; set; } = string.Empty;
}

public record SupportTicketListItemDto(
    int SupportTicketId,
    string TicketCode,
    string Title,
    string Status,
    string CreatedByName,
    string? AssignedAdminName,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    DateTime? ResolvedAt,
    int MessageCount
);

public record SupportTicketDetailsDto(
    int SupportTicketId,
    string TicketCode,
    string Title,
    string Description,
    string Status,
    int CreatedByUserId,
    string CreatedByName,
    int? AssignedAdminUserId,
    string? AssignedAdminName,
    string? ResolutionSummary,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    DateTime? ResolvedAt,
    bool CanChat,
    bool CanAccept,
    bool CanResolve
);

public record SupportTicketMessageDto(
    long SupportTicketMessageId,
    int SupportTicketId,
    int SenderUserId,
    string SenderName,
    bool IsFromAdmin,
    string Body,
    DateTime CreatedAt
);

public record AdminNotificationDto(
    long AdminNotificationId,
    int? SupportTicketId,
    string Type,
    string Title,
    string Body,
    bool IsRead,
    DateTime CreatedAt
);
