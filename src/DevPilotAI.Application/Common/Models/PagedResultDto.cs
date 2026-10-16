namespace DevPilotAI.Application.Common.Models;

public record PagedResultDto<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);
