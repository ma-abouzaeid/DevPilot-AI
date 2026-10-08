using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Domain.Entities;

public class EmailVerification
{
    [Key]
    public int VerificationId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string CodeHash { get; set; } = string.Empty;
    public string? TokenHash { get; set; }
    public string Method { get; set; } = "OTP";
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; } = 0;
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
