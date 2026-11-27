using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Feedback;

public class CreateFeedbackRequestDto
{
    [Required]
    [StringLength(4000, MinimumLength = 5)]
    public string Message { get; set; } = string.Empty;

    [StringLength(80)]
    public string Category { get; set; } = "General";
}

public record FeedbackListItemDto(
    long UserFeedbackId,
    int? UserId,
    string? UserEmail,
    string Source,
    string Category,
    string Message,
    string Status,
    DateTime CreatedAt
);
