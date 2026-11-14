using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Plans.DTOs;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Plans;

public class PlanUsageService : IPlanUsageService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService? _auditService;

    public PlanUsageService(IApplicationDbContext db, IAuditService? auditService = null)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<PlanListItemDto>> ListPublicPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _db.Plans.AsNoTracking()
            .Where(p => p.IsPublic && p.Status == "Active")
            .OrderBy(p => p.Price ?? 0)
            .ThenBy(p => p.PlanId)
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .ToListAsync(cancellationToken);

        return plans.Select(p => new PlanListItemDto(
            p.PlanId,
            p.Code,
            p.Name,
            p.Description,
            p.CreditsPerPeriod,
            p.Price,
            p.Currency,
            p.IsDefault,
            p.PlanFeatures
                .Where(pf => pf.IsEnabled)
                .Select(pf => pf.Feature.Key)
                .OrderBy(k => k)
                .ToList()
        )).ToList();
    }

    public async Task<MyPlanUsageDto> GetMyUsageAsync(int userId, CancellationToken cancellationToken = default)
    {
        var period = await EnsureCurrentPeriodAsync(userId, cancellationToken);
        var plan = await _db.Plans.AsNoTracking()
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .FirstAsync(p => p.PlanId == period.PlanId, cancellationToken);

        var remaining = Math.Max(0, period.CreditsAllocated - period.CreditsUsed + period.CreditsRefunded);
        var features = plan.PlanFeatures
            .Select(pf => new PlanFeatureDto(pf.Feature.Key, pf.Feature.Name, pf.IsEnabled, pf.LimitValue))
            .OrderBy(f => f.Key)
            .ToList();

        var canReview = features.Any(f =>
            f.Key == PlanFeatureKeys.GitHubCodeReview && f.IsEnabled);

        return new MyPlanUsageDto(
            PlanCode: plan.Code,
            PlanName: plan.Name,
            Price: plan.Price,
            Currency: plan.Currency,
            CreditsAllocated: period.CreditsAllocated,
            CreditsUsed: period.CreditsUsed,
            CreditsRemaining: remaining,
            PeriodStart: period.PeriodStart,
            PeriodEnd: period.PeriodEnd,
            CanUseCodeReview: canReview,
            Features: features
        );
    }

    public async Task<UsagePeriod> EnsureCurrentPeriodAsync(int userId, CancellationToken cancellationToken = default)
    {
        var subscription = await GetActiveSubscriptionAsync(userId, cancellationToken);
        var plan = await _db.Plans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlanId == subscription.PlanId, cancellationToken)
            ?? throw AppError.NotFound("Active plan was not found.");

        var (start, end) = CurrentMonthBoundsUtc();
        var period = await _db.UsagePeriods
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.PeriodStart == start &&
                p.PeriodEnd == end,
                cancellationToken);

        if (period != null)
        {
            // If the user upgraded mid-month, bump allocation to the current plan.
            if (period.PlanId != plan.PlanId || period.CreditsAllocated < plan.CreditsPerPeriod)
            {
                period.PlanId = plan.PlanId;
                period.CreditsAllocated = plan.CreditsPerPeriod;
                period.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return period;
        }

        period = new UsagePeriod
        {
            UserId = userId,
            PlanId = plan.PlanId,
            PeriodStart = start,
            PeriodEnd = end,
            CreditsAllocated = plan.CreditsPerPeriod,
            CreditsReserved = 0,
            CreditsUsed = 0,
            CreditsRefunded = 0,
            RequestCount = 0,
            TokenCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.UsagePeriods.Add(period);
        await _db.SaveChangesAsync(cancellationToken);
        return period;
    }

    public async Task EnsureFeatureAsync(int userId, string featureKey, CancellationToken cancellationToken = default)
    {
        if (!await HasFeatureAsync(userId, featureKey, cancellationToken))
        {
            throw AppError.Forbidden(
                featureKey == PlanFeatureKeys.GitHubCodeReview
                    ? "GitHub Code Review is available on the Pro plan. Upgrade to unlock this feature."
                    : $"Your plan does not include '{featureKey}'.",
                featureKey == PlanFeatureKeys.GitHubCodeReview
                    ? "PLAN_FEATURE_REQUIRED"
                    : "FEATURE_NOT_AVAILABLE");
        }
    }

    public async Task<bool> HasFeatureAsync(int userId, string featureKey, CancellationToken cancellationToken = default)
    {
        var subscription = await GetActiveSubscriptionAsync(userId, cancellationToken);
        return await _db.PlanFeatures.AsNoTracking()
            .AnyAsync(pf =>
                pf.PlanId == subscription.PlanId &&
                pf.IsEnabled &&
                pf.Feature.Key == featureKey,
                cancellationToken);
    }

    public async Task EnsureCreditsAsync(int userId, int amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return;

        var period = await EnsureCurrentPeriodAsync(userId, cancellationToken);
        var remaining = period.CreditsAllocated - period.CreditsUsed + period.CreditsRefunded;
        if (remaining < amount)
        {
            throw AppError.Forbidden(
                $"Not enough credits. This action needs {amount} credits; you have {Math.Max(0, remaining)} left this period.",
                "INSUFFICIENT_CREDITS");
        }
    }

    public async Task ChargeCreditsAsync(
        int userId,
        int amount,
        string reason,
        string? referenceId = null,
        long? aiUsageEventId = null,
        string? metadataJson = null,
        int tokensUsed = 0,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return;

        await EnsureCreditsAsync(userId, amount, cancellationToken);
        var period = await EnsureCurrentPeriodAsync(userId, cancellationToken);

        period.CreditsUsed += amount;
        period.RequestCount += 1;
        if (tokensUsed > 0)
        {
            period.TokenCount += tokensUsed;
        }
        period.UpdatedAt = DateTime.UtcNow;

        if (aiUsageEventId.HasValue)
        {
            var usageEvent = await _db.AIUsageEvents
                .FirstOrDefaultAsync(e => e.AIUsageEventId == aiUsageEventId.Value, cancellationToken);
            if (usageEvent != null)
            {
                usageEvent.CreditsCharged = (usageEvent.CreditsCharged ?? 0) + amount;
                usageEvent.CostType = "Credits";
                if (string.Equals(usageEvent.Status, "Success", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(usageEvent.Status))
                {
                    usageEvent.Status = "Charged";
                }
            }
        }

        _db.UsageLedgers.Add(new UsageLedger
        {
            UserId = userId,
            UsagePeriodId = period.UsagePeriodId,
            AIUsageEventId = aiUsageEventId,
            EntryType = "CHARGE",
            Amount = amount,
            Reason = Truncate(reason, 255),
            ReferenceId = string.IsNullOrWhiteSpace(referenceId) ? null : Truncate(referenceId, 150),
            MetadataJson = metadataJson,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SystemAuditEventTypes.CreditsCharged,
                nameof(UsageLedger),
                userId.ToString(),
                oldValues: null,
                newValues: new
                {
                    UserId = userId,
                    Amount = amount,
                    Reason = reason,
                    ReferenceId = referenceId,
                    AIUsageEventId = aiUsageEventId,
                    TokensUsed = tokensUsed,
                    RemainingAfter = Math.Max(0, period.CreditsAllocated - period.CreditsUsed + period.CreditsRefunded)
                },
                userId: userId,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<MyPlanUsageDto> ActivatePlanAsync(int userId, string planCode, CancellationToken cancellationToken = default)
    {
        var code = (planCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw AppError.InvalidRequest("Plan code is required.", "INVALID_REQUEST", nameof(planCode));
        }

        var plan = await _db.Plans
            .FirstOrDefaultAsync(p => p.Code == code && p.Status == "Active" && p.IsPublic, cancellationToken)
            ?? throw AppError.NotFound($"Plan '{code}' was not found.");

        var active = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "Active")
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);

        foreach (var sub in active)
        {
            if (sub.PlanId == plan.PlanId)
            {
                await EnsureCurrentPeriodAsync(userId, cancellationToken);
                return await GetMyUsageAsync(userId, cancellationToken);
            }

            sub.Status = "Cancelled";
            sub.CancelledAt = DateTime.UtcNow;
            sub.EndDate = DateTime.UtcNow;
            sub.CancellationReason = $"Switched to {plan.Code}";
            sub.UpdatedAt = DateTime.UtcNow;
        }

        _db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = plan.PlanId,
            Status = "Active",
            StartDate = DateTime.UtcNow,
            AssignmentSource = "SelfServe",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        // Refresh current period allocation for the new plan.
        await EnsureCurrentPeriodAsync(userId, cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SystemAuditEventTypes.PlanActivated,
                nameof(UserSubscription),
                userId.ToString(),
                oldValues: null,
                newValues: new { UserId = userId, PlanCode = plan.Code, PlanId = plan.PlanId, Source = "SelfServe" },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return await GetMyUsageAsync(userId, cancellationToken);
    }

    private async Task<UserSubscription> GetActiveSubscriptionAsync(int userId, CancellationToken cancellationToken)
    {
        var subscription = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "Active")
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription != null) return subscription;

        var defaultPlan = await _db.Plans
            .FirstOrDefaultAsync(p => p.IsDefault && p.Status == "Active", cancellationToken)
            ?? throw AppError.NotFound("No default plan is configured.");

        subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = defaultPlan.PlanId,
            Status = "Active",
            StartDate = DateTime.UtcNow,
            AssignmentSource = "System",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.UserSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    private static (DateTime Start, DateTime End) CurrentMonthBoundsUtc()
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
