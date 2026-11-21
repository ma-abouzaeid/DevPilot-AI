using DevPilotAI.Application.Common;
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
                .ToList(),
            p.SaleEnabled,
            p.SaleDiscountPercent,
            p.GetEffectivePriceUsd()
        )).ToList();
    }

    public async Task<MyPlanUsageDto> GetMyUsageAsync(int userId, CancellationToken cancellationToken = default)
    {
        var period = await EnsureCurrentPeriodAsync(userId, cancellationToken);
        var plan = await _db.Plans.AsNoTracking()
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .FirstAsync(p => p.PlanId == period.PlanId, cancellationToken);

        var unlimited = await HasUnlimitedCreditsAsync(userId, cancellationToken);
        var remaining = unlimited
            ? int.MaxValue
            : Math.Max(0, period.CreditsAllocated - period.CreditsUsed + period.CreditsRefunded);
        var features = plan.PlanFeatures
            .Select(pf => new PlanFeatureDto(pf.Feature.Key, pf.Feature.Name, pf.IsEnabled, pf.LimitValue))
            .OrderBy(f => f.Key)
            .ToList();

        var canReview = await IsAdminAsync(userId, cancellationToken) || features.Any(f =>
            f.Key == PlanFeatureKeys.GitHubCodeReview && f.IsEnabled);

        return new MyPlanUsageDto(
            PlanCode: plan.Code,
            PlanName: plan.Name,
            Price: plan.Price,
            Currency: plan.Currency,
            CreditsAllocated: unlimited ? int.MaxValue : period.CreditsAllocated,
            CreditsUsed: period.CreditsUsed,
            CreditsRemaining: remaining,
            PeriodStart: period.PeriodStart,
            PeriodEnd: period.PeriodEnd,
            CanUseCodeReview: canReview,
            Features: features,
            UnlimitedCredits: unlimited
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
            // Mid-month plan change: upgrading grants a full fresh credit pool.
            if (period.PlanId != plan.PlanId || period.CreditsAllocated != plan.CreditsPerPeriod)
            {
                var isUpgrade = plan.CreditsPerPeriod > period.CreditsAllocated;
                period.PlanId = plan.PlanId;
                period.CreditsAllocated = plan.CreditsPerPeriod;
                if (isUpgrade)
                {
                    period.CreditsUsed = 0;
                    period.CreditsRefunded = 0;
                    period.CreditsReserved = 0;
                }
                else if (period.CreditsUsed > period.CreditsAllocated)
                {
                    period.CreditsUsed = period.CreditsAllocated;
                }

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
        if (await IsAdminAsync(userId, cancellationToken))
        {
            return true;
        }

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
        if (await HasUnlimitedCreditsAsync(userId, cancellationToken)) return;

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

        var unlimited = await HasUnlimitedCreditsAsync(userId, cancellationToken);
        if (!unlimited)
        {
            await EnsureCreditsAsync(userId, amount, cancellationToken);
        }

        var period = await EnsureCurrentPeriodAsync(userId, cancellationToken);

        // Unlimited accounts keep balance; still record usage for audit/AI tracking.
        if (!unlimited)
        {
            period.CreditsUsed += amount;
        }

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
                usageEvent.CostType = unlimited ? "Unlimited" : "Credits";
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
            EntryType = unlimited ? "UNLIMITED" : "CHARGE",
            Amount = amount,
            Reason = Truncate(unlimited ? $"Unlimited: {reason}" : reason, 255),
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
                    Unlimited = unlimited,
                    RemainingAfter = unlimited
                        ? (int?)null
                        : Math.Max(0, period.CreditsAllocated - period.CreditsUsed + period.CreditsRefunded)
                },
                userId: userId,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<MyPlanUsageDto> ActivatePlanAsync(
        int userId,
        string planCode,
        CancellationToken cancellationToken = default,
        string assignmentSource = "SelfServe")
    {
        var code = (planCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw AppError.InvalidRequest("Plan code is required.", "INVALID_REQUEST", nameof(planCode));
        }

        var source = string.IsNullOrWhiteSpace(assignmentSource) ? "SelfServe" : assignmentSource.Trim();

        var plan = await _db.Plans
            .FirstOrDefaultAsync(p => p.Code == code && p.Status == "Active" && p.IsPublic, cancellationToken)
            ?? throw AppError.NotFound($"Plan '{code}' was not found.");

        // Paid plans cannot be activated via plain self-serve activate — must go through Paymob.
        if ((plan.Price ?? 0m) > 0m &&
            string.Equals(source, "SelfServe", StringComparison.OrdinalIgnoreCase))
        {
            throw AppError.InvalidRequest(
                "Paid plans require Paymob checkout.",
                "PAYMENT_REQUIRED",
                nameof(planCode));
        }

        var active = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "Active")
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);

        // Do not let users cancel a paid plan by switching to Free from the Plans page.
        if (string.Equals(source, "SelfServe", StringComparison.OrdinalIgnoreCase) &&
            (plan.Price ?? 0m) <= 0m)
        {
            var activePlanIds = active.Select(s => s.PlanId).Distinct().ToList();
            var hasPaidPlan = await _db.Plans.AsNoTracking()
                .AnyAsync(p => activePlanIds.Contains(p.PlanId) && (p.Price ?? 0m) > 0m, cancellationToken);
            if (hasPaidPlan)
            {
                throw AppError.InvalidRequest(
                    "Your Pro subscription stays active. Contact Support to cancel or change it.",
                    "PAID_PLAN_LOCKED",
                    nameof(planCode));
            }
        }

        var now = DateTime.UtcNow;
        foreach (var sub in active)
        {
            if (sub.PlanId == plan.PlanId)
            {
                // Keep paid coverage window if missing (older rows).
                if (sub.EndDate == null)
                {
                    var planned = plan.ComputeSubscriptionEndDate(sub.StartDate);
                    if (planned.HasValue)
                    {
                        sub.EndDate = planned;
                        sub.UpdatedAt = now;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }

                await EnsureCurrentPeriodAsync(userId, cancellationToken);
                return await GetMyUsageAsync(userId, cancellationToken);
            }

            sub.Status = "Cancelled";
            sub.CancelledAt = now;
            // Access ended now (may be earlier than a previously scheduled paid EndDate).
            sub.EndDate = now;
            sub.CancellationReason = $"Switched to {plan.Code}";
            sub.UpdatedAt = now;
        }

        _db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = plan.PlanId,
            Status = "Active",
            StartDate = now,
            EndDate = plan.ComputeSubscriptionEndDate(now),
            AssignmentSource = source.Length > 50 ? source[..50] : source,
            CreatedAt = now,
            UpdatedAt = now
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
                newValues: new { UserId = userId, PlanCode = plan.Code, PlanId = plan.PlanId, Source = source },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return await GetMyUsageAsync(userId, cancellationToken);
    }

    private async Task<bool> IsAdminAsync(int userId, CancellationToken cancellationToken)
    {
        return await _db.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == AppRoles.Admin, cancellationToken);
    }

    private async Task<bool> HasUnlimitedCreditsAsync(int userId, CancellationToken cancellationToken)
    {
        if (await IsAdminAsync(userId, cancellationToken))
        {
            return true;
        }

        return await _db.Users.AsNoTracking()
            .AnyAsync(u => u.UserId == userId && u.HasUnlimitedCredits, cancellationToken);
    }

    private async Task<UserSubscription> GetActiveSubscriptionAsync(int userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var subscription = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "Active")
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription != null)
        {
            // Paid period ended → expire and fall through to Free/default.
            if (subscription.EndDate.HasValue && subscription.EndDate.Value <= now)
            {
                subscription.Status = "Expired";
                subscription.CancelledAt ??= now;
                subscription.CancellationReason ??= "Subscription period ended";
                subscription.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                subscription = null;
            }
            else
            {
                return subscription;
            }
        }

        var defaultPlan = await _db.Plans
            .FirstOrDefaultAsync(p => p.IsDefault && p.Status == "Active", cancellationToken)
            ?? throw AppError.NotFound("No default plan is configured.");

        subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = defaultPlan.PlanId,
            Status = "Active",
            StartDate = now,
            EndDate = defaultPlan.ComputeSubscriptionEndDate(now),
            AssignmentSource = "System",
            CreatedAt = now,
            UpdatedAt = now
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
