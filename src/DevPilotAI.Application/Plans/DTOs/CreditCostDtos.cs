namespace DevPilotAI.Application.Plans.DTOs;

public record CreditCostItemDto(
    string ActionKey,
    string DisplayName,
    int Credits,
    bool ShowOnPlansPage,
    int SortOrder
);

public class UpdateCreditCostItemDto
{
    public string ActionKey { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool? ShowOnPlansPage { get; set; }
}

public class UpdateCreditCostsRequestDto
{
    public List<UpdateCreditCostItemDto> Items { get; set; } = [];
}
