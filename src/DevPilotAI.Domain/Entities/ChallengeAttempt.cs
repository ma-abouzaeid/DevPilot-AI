namespace DevPilotAI.Domain.Entities;

public class ChallengeAttempt
{
    public int ChallengeAttemptId { get; set; }
    public int ChallengeId { get; set; }
    public Challenge Challenge { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string? GitHubRepoUrl { get; set; }
    public string Status { get; set; } = "InProgress";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    public ChallengeEvaluation? Evaluation { get; set; }
}
