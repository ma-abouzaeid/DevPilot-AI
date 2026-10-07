namespace DevPilotAI.Domain.Entities;

public class ChallengeEvaluation
{
    public int ChallengeEvaluationId { get; set; }
    public int ChallengeAttemptId { get; set; }
    public ChallengeAttempt ChallengeAttempt { get; set; } = null!;

    public int CorrectnessScore { get; set; }
    public int CodeQualityScore { get; set; }
    public int SecurityScore { get; set; }
    public string FeedbackSummary { get; set; } = string.Empty;
    public string DetailedReport { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}
