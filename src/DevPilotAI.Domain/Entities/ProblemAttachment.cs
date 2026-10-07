namespace DevPilotAI.Domain.Entities;

public class ProblemAttachment
{
    public int ProblemAttachmentId { get; set; }
    public int ProblemRequestId { get; set; }
    public ProblemRequest ProblemRequest { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
