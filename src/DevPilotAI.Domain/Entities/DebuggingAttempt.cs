namespace DevPilotAI.Domain.Entities;

public class DebuggingAttempt
{
    public int DebuggingAttemptId { get; set; }
    public int ProblemId { get; set; }
    public Problem Problem { get; set; } = null!;

    public int ProblemRequestId { get; set; }
    public ProblemRequest ProblemRequest { get; set; } = null!;

    public string RootCauseAnalysis { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string ProposedSolution { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DeveloperFeedback> Feedbacks { get; set; } = new List<DeveloperFeedback>();
    public ICollection<AIRequest> AIRequests { get; set; } = new List<AIRequest>();
}
