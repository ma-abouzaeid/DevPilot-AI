using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class GitHubRepository : ISoftDeletable
{
    public int GitHubRepositoryId { get; set; }
    public int GitHubAccountId { get; set; }
    public GitHubAccount GitHubAccount { get; set; } = null!;

    public string RepoName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RepoUrl { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public bool IsPrivate { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public ICollection<CodeReview> CodeReviews { get; set; } = new List<CodeReview>();
}
