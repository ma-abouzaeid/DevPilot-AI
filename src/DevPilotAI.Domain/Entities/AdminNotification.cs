namespace DevPilotAI.Domain.Entities;

public class AdminNotification
{
    public long AdminNotificationId { get; set; }
    public int AdminUserId { get; set; }
    public User AdminUser { get; set; } = null!;
    public int? SupportTicketId { get; set; }
    public SupportTicket? SupportTicket { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
