using DevPilotAI.Domain.Common;

namespace DevPilotAI.Domain.Entities;

public class ProblemAttachment : ISoftDeletable
{
    public int ProblemAttachmentId { get; set; }
    public int ProblemRequestId { get; set; }
    public ProblemRequest ProblemRequest { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
