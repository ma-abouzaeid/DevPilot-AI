namespace DevPilotAI.Domain.Entities;

public class SupportTicketMessage
{
    public long SupportTicketMessageId { get; set; }
    public int SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = null!;
    public int SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;
    // Sent from admin UI, not inferred from user id.
    public bool IsFromAdmin { get; set; }
    public string Body { get; set; } = string.Empty;
    // e.g. payment proof image path
    public string? AttachmentUrl { get; set; }
    public string? AttachmentContentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
