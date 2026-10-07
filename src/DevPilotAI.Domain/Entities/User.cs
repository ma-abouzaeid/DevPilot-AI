namespace DevPilotAI.Domain.Entities;

public class User
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Problem> Problems { get; set; } = new List<Problem>();
    public ICollection<GitHubAccount> GitHubAccounts { get; set; } = new List<GitHubAccount>();
    public ICollection<CodeReview> CodeReviews { get; set; } = new List<CodeReview>();
    public ICollection<ChallengeAttempt> ChallengeAttempts { get; set; } = new List<ChallengeAttempt>();
    public ICollection<AIRequest> AIRequests { get; set; } = new List<AIRequest>();
}
