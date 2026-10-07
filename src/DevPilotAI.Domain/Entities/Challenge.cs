using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class Challenge
{
    public int ChallengeId { get; set; }
    public int ProblemId { get; set; }
    public Problem Problem { get; set; } = null!;

    public int? KnowledgeArticleId { get; set; }
    public KnowledgeArticle? KnowledgeArticle { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
    public string StarterCode { get; set; } = string.Empty;
    public ChallengeDifficulty Difficulty { get; set; } = ChallengeDifficulty.Intermediate;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChallengeAttempt> Attempts { get; set; } = new List<ChallengeAttempt>();
}
