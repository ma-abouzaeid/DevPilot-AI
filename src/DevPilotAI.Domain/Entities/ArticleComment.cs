using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ArticleComment : ISoftDeletable
{
    public long ArticleCommentId { get; set; }
    public int KnowledgeArticleId { get; set; }
    public KnowledgeArticle KnowledgeArticle { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
