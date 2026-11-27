using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Feedback;

public class FeedbackService : IFeedbackService
{
    private readonly IApplicationDbContext _db;

    public FeedbackService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<FeedbackListItemDto> CreateAsync(
        int? userId,
        CreateFeedbackRequestDto request,
        string source,
        CancellationToken cancellationToken = default)
    {
        var message = (request.Message ?? string.Empty).Trim();
        if (message.Length < 5)
        {
            throw AppError.InvalidRequest("Feedback message is required.", "INVALID_REQUEST");
        }

        byte? rating = null;
        if (request.Rating is >= 1 and <= 5)
            rating = request.Rating;

        var entity = new UserFeedback
        {
            UserId = userId,
            Source = string.IsNullOrWhiteSpace(source) ? "Web" : source.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Message = message.Length > 4000 ? message[..4000] : message,
            Rating = rating,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };
        _db.UserFeedbacks.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        string? email = null;
        if (userId.HasValue)
        {
            email = await _db.Users.AsNoTracking()
                .Where(u => u.UserId == userId.Value)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Map(entity, email);
    }

    public async Task<IReadOnlyList<FeedbackListItemDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserFeedbacks.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(50)
            .Select(f => new FeedbackListItemDto(
                f.UserFeedbackId,
                f.UserId,
                f.User != null ? f.User.Email : null,
                f.Source,
                f.Category,
                f.Message,
                f.Rating,
                f.Status,
                f.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FeedbackListItemDto>> ListAllAsync(string? status, CancellationToken cancellationToken = default)
    {
        var query = _db.UserFeedbacks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            query = query.Where(f => f.Status == s);
        }

        return await query
            .OrderByDescending(f => f.CreatedAt)
            .Take(200)
            .Select(f => new FeedbackListItemDto(
                f.UserFeedbackId,
                f.UserId,
                f.User != null ? f.User.Email : null,
                f.Source,
                f.Category,
                f.Message,
                f.Rating,
                f.Status,
                f.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static FeedbackListItemDto Map(UserFeedback entity, string? email) =>
        new(
            entity.UserFeedbackId,
            entity.UserId,
            email,
            entity.Source,
            entity.Category,
            entity.Message,
            entity.Rating,
            entity.Status,
            entity.CreatedAt);

    public async Task MarkReviewedAsync(long feedbackId, int adminUserId, CancellationToken cancellationToken = default)
    {
        var item = await _db.UserFeedbacks.FirstOrDefaultAsync(f => f.UserFeedbackId == feedbackId, cancellationToken)
            ?? throw AppError.NotFound("Feedback was not found.");
        item.Status = "Reviewed";
        item.ReviewedAt = DateTime.UtcNow;
        item.ReviewedByUserId = adminUserId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
