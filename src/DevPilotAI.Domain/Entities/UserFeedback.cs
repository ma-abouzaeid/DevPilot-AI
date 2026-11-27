namespace DevPilotAI.Domain.Entities;

public class UserFeedback
{
    public long UserFeedbackId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public long? TelegramChatId { get; set; }
    public string Source { get; set; } = "Web";
    public string Category { get; set; } = "General";
    public string Message { get; set; } = string.Empty;
    /// <summary>Optional 1–5 star rating from the web feedback form.</summary>
    public byte? Rating { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
}
