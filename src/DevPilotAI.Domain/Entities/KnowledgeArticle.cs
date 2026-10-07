namespace DevPilotAI.Domain.Entities;

public class KnowledgeArticle
{
    public int KnowledgeArticleId { get; set; }
    public int ProblemId { get; set; }
    public Problem Problem { get; set; } = null!;

    public string ArticleCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int CurrentVersion { get; set; } = 1;
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ArticleVersion> Versions { get; set; } = new List<ArticleVersion>();
    public ICollection<Challenge> Challenges { get; set; } = new List<Challenge>();
}
