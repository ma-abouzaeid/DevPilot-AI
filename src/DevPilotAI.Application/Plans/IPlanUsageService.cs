using DevPilotAI.Application.Plans.DTOs;
using DevPilotAI.Domain.Entities;

namespace DevPilotAI.Application.Plans;

public interface IPlanUsageService
{
    Task<IReadOnlyList<PlanListItemDto>> ListPublicPlansAsync(CancellationToken cancellationToken = default);
    Task<MyPlanUsageDto> GetMyUsageAsync(int userId, CancellationToken cancellationToken = default);
    Task<UsagePeriod> EnsureCurrentPeriodAsync(int userId, CancellationToken cancellationToken = default);
    Task EnsureFeatureAsync(int userId, string featureKey, CancellationToken cancellationToken = default);
    Task<bool> HasFeatureAsync(int userId, string featureKey, CancellationToken cancellationToken = default);
    Task ChargeCreditsAsync(int userId, int amount, string reason, string? referenceId = null, CancellationToken cancellationToken = default);
    Task<MyPlanUsageDto> ActivatePlanAsync(int userId, string planCode, CancellationToken cancellationToken = default);
}
