namespace DevPilotAI.Application.Feedback;

public interface IFeedbackService
{
    Task<FeedbackListItemDto> CreateAsync(int? userId, CreateFeedbackRequestDto request, string source, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedbackListItemDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedbackListItemDto>> ListAllAsync(string? status, CancellationToken cancellationToken = default);
    Task MarkReviewedAsync(long feedbackId, int adminUserId, CancellationToken cancellationToken = default);
}
