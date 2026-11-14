using DevPilotAI.Application.Plans.DTOs;

namespace DevPilotAI.Application.Plans;

public interface ICreditCostService
{
    Task<int> GetCostAsync(string actionKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditCostItemDto>> ListPublicAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditCostItemDto>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CreditCostItemDto>> UpdateAsync(
        IReadOnlyList<UpdateCreditCostItemDto> items,
        CancellationToken cancellationToken = default);
}
