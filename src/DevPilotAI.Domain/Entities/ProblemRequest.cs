namespace DevPilotAI.Domain.Entities;

public class ProblemRequest
{
    public int ProblemRequestId { get; set; }
    public int ProblemId { get; set; }
    public Problem Problem { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? SourceCode { get; set; }
    public string? Logs { get; set; }
    public string? Framework { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProblemAttachment> Attachments { get; set; } = new List<ProblemAttachment>();
    public ICollection<DebuggingAttempt> DebuggingAttempts { get; set; } = new List<DebuggingAttempt>();
}
