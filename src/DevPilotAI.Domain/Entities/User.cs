using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class User : ISoftDeletable
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime? EmailVerifiedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Problem> Problems { get; set; } = new List<Problem>();
    public ICollection<GitHubAccount> GitHubAccounts { get; set; } = new List<GitHubAccount>();
    public ICollection<CodeReview> CodeReviews { get; set; } = new List<CodeReview>();
    public ICollection<ChallengeAttempt> ChallengeAttempts { get; set; } = new List<ChallengeAttempt>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    public ICollection<AIUsageEvent> AIUsageEvents { get; set; } = new List<AIUsageEvent>();
    public ICollection<UsagePeriod> UsagePeriods { get; set; } = new List<UsagePeriod>();
    public ICollection<UsageLedger> UsageLedgers { get; set; } = new List<UsageLedger>();
}
