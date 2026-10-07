using DevPilotAI.Domain.Enums;

namespace DevPilotAI.Domain.Entities;

public class CodeReviewFinding
{
    public int CodeReviewFindingId { get; set; }
    public int CodeReviewId { get; set; }
    public CodeReview CodeReview { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;
    public int? LineNumber { get; set; }
    public ReviewSeverity Severity { get; set; } = ReviewSeverity.Medium;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
}
