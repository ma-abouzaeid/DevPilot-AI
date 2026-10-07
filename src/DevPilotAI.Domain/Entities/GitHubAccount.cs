namespace DevPilotAI.Domain.Entities;

public class GitHubAccount
{
    public int GitHubAccountId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string GitHubUsername { get; set; } = string.Empty;
    public string GitHubUserId { get; set; } = string.Empty;
    public string AccessTokenEncrypted { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GitHubRepository> Repositories { get; set; } = new List<GitHubRepository>();
}
