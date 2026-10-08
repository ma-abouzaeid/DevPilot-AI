using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ProblemSolution : ISoftDeletable
{
    public int ProblemSolutionId { get; set; }
    public int ProblemId { get; set; }
    public Problem Problem { get; set; } = null!;

    public int SuccessfulAttemptId { get; set; }
    public DebuggingAttempt SuccessfulAttempt { get; set; } = null!;

    public string RootCause { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DetailedSteps { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime SolvedAt { get; set; } = DateTime.UtcNow;
}
