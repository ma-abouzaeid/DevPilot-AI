using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Domain.Entities;

public class RefreshToken
{
    [Key]
    public int RefreshTokenId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public string? ReplacedByTokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
