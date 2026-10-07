namespace DevPilotAI.Domain.Entities;

public class ProblemSolution
{
    public int ProblemSolutionId { get; set; }
    public int ProblemId { get; set; }
    public Problem Problem { get; set; } = null!;

    public int SuccessfulAttemptId { get; set; }
    public DebuggingAttempt SuccessfulAttempt { get; set; } = null!;

    public string RootCause { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DetailedSteps { get; set; } = string.Empty;
    public DateTime SolvedAt { get; set; } = DateTime.UtcNow;
}
