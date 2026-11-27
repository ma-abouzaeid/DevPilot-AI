namespace DevPilotAI.Domain.Entities;

public class SupportTicketMessage
{
    public long SupportTicketMessageId { get; set; }
    public int SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = null!;
    public int SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;
    /// <summary>True when sent from the admin ticket workspace (not inferred from user id).</summary>
    public bool IsFromAdmin { get; set; }
    public string Body { get; set; } = string.Empty;
    /// <summary>Relative path or URL for ticket-only attachments (e.g. payment proof).</summary>
    public string? AttachmentUrl { get; set; }
    public string? AttachmentContentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
