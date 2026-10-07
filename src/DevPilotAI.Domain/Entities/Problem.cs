using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class Problem
{
    public int ProblemId { get; set; }
    public string ProblemCode { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public ProblemStatus Status { get; set; } = ProblemStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public ICollection<ProblemRequest> ProblemRequests { get; set; } = new List<ProblemRequest>();
    public ICollection<DebuggingAttempt> DebuggingAttempts { get; set; } = new List<DebuggingAttempt>();
    public ICollection<ProblemSolution> ProblemSolutions { get; set; } = new List<ProblemSolution>();
    public ICollection<KnowledgeArticle> KnowledgeArticles { get; set; } = new List<KnowledgeArticle>();
    public ICollection<Challenge> Challenges { get; set; } = new List<Challenge>();
}
