namespace DevPilotAI.Domain.Entities;

/// <summary>Telegram bot state per chat.</summary>
public class TelegramConversation
{
    public long TelegramConversationId { get; set; }
    public long ChatId { get; set; }
    public int? LinkedUserId { get; set; }
    public User? LinkedUser { get; set; }
    public string State { get; set; } = "Idle";
    public string? PayloadJson { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
