namespace DevPilotAI.Application.Plans.DTOs;

public record PlanListItemDto(
    int PlanId,
    string Code,
    string Name,
    string? Description,
    int CreditsPerPeriod,
    decimal? Price,
    string? Currency,
    bool IsDefault,
    IReadOnlyList<string> Features
);

public record PlanFeatureDto(
    string Key,
    string Name,
    bool IsEnabled,
    int? LimitValue
);

public record MyPlanUsageDto(
    string PlanCode,
    string PlanName,
    decimal? Price,
    string? Currency,
    int CreditsAllocated,
    int CreditsUsed,
    int CreditsRemaining,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    bool CanUseCodeReview,
    IReadOnlyList<PlanFeatureDto> Features
);

public record ActivatePlanRequestDto(
    string PlanCode
);
