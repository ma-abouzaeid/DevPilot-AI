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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
