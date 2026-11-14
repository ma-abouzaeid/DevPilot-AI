namespace DevPilotAI.Domain.Entities;

public class SimilarityMatch
{
    public long SimilarityMatchId { get; set; }
    public int ProblemRequestId { get; set; }
    public ProblemRequest ProblemRequest { get; set; } = null!;

    public int MatchedProblemId { get; set; }
    public Problem MatchedProblem { get; set; } = null!;

    public decimal SimilarityScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
