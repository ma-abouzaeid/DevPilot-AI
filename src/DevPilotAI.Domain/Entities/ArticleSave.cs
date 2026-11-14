namespace DevPilotAI.Domain.Entities;

public class ArticleSave
{
    public long ArticleSaveId { get; set; }
    public int KnowledgeArticleId { get; set; }
    public KnowledgeArticle KnowledgeArticle { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
