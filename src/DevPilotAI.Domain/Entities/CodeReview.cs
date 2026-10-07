namespace DevPilotAI.Domain.Entities;

public class CodeReview
{
    public int CodeReviewId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int? GitHubRepositoryId { get; set; }
    public GitHubRepository? GitHubRepository { get; set; }

    public string BranchName { get; set; } = "main";
    public string TargetTargetRef { get; set; } = string.Empty;
    public string ReviewSummary { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CodeReviewFinding> Findings { get; set; } = new List<CodeReviewFinding>();
}
