using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class DeveloperFeedback
{
    public int DeveloperFeedbackId { get; set; }
    public int DebuggingAttemptId { get; set; }
    public DebuggingAttempt DebuggingAttempt { get; set; } = null!;

    public AttemptResult FeedbackResult { get; set; }
    public string? AdditionalComments { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
