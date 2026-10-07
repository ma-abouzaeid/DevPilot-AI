namespace DevPilotAI.Domain.Entities;

public class ArticleVersion
{
    public int ArticleVersionId { get; set; }
    public int KnowledgeArticleId { get; set; }
    public KnowledgeArticle KnowledgeArticle { get; set; } = null!;

    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
