using DevPilotAI.Application.Common.Models;

namespace DevPilotAI.Application.Admin;

public interface IAdminService
{
    Task<AdminOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<PagedResultDto<AdminUserListItemDto>> ListUsersAsync(
        string? search,
        bool includeDeleted,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailDto> GetUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<AdminUserDetailDto> CreateUserAsync(int adminUserId, CreateAdminUserRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminUserDetailDto> UpdateUserAsync(int adminUserId, int userId, UpdateAdminUserRequestDto request, CancellationToken cancellationToken = default);
    Task DeactivateUserAsync(int adminUserId, int userId, CancellationToken cancellationToken = default);
    Task RestoreUserAsync(int adminUserId, int userId, CancellationToken cancellationToken = default);
    Task AssignUserPlanAsync(int adminUserId, int userId, AssignUserPlanRequestDto request, CancellationToken cancellationToken = default);
    Task SetUserAdminAsync(int adminUserId, int userId, SetUserAdminRequestDto request, CancellationToken cancellationToken = default);
    Task SetUserUnlimitedCreditsAsync(int adminUserId, int userId, SetUserUnlimitedCreditsRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminPlanDto>> ListPlansAsync(CancellationToken cancellationToken = default);
    Task<AdminPlanDto> CreatePlanAsync(int adminUserId, CreateAdminPlanRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminPlanDto> UpdatePlanAsync(int adminUserId, int planId, UpdateAdminPlanRequestDto request, CancellationToken cancellationToken = default);

    Task<PagedResultDto<AdminArticleListItemDto>> ListArticlesAsync(
        string? search,
        bool? isPublished,
        bool includeDeleted,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminArticleDetailDto> GetArticleAsync(int articleId, CancellationToken cancellationToken = default);
    Task<AdminArticleDetailDto> UpdateArticleAsync(int adminUserId, int articleId, UpdateAdminArticleRequestDto request, CancellationToken cancellationToken = default);
    Task SetArticlePublishedAsync(int adminUserId, int articleId, bool published, CancellationToken cancellationToken = default);
    Task SoftDeleteArticleAsync(int adminUserId, int articleId, CancellationToken cancellationToken = default);
    Task RestoreArticleAsync(int adminUserId, int articleId, CancellationToken cancellationToken = default);

    Task<PagedResultDto<AdminAuditLogDto>> ListAuditLogsAsync(
        int? userId,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<AdminAiUsageDto>> ListAiUsageAsync(
        int? userId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminAiUsageCapacityDto> GetAiUsageCapacityAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<AdminSystemErrorDto>> ListSystemErrorsAsync(
        int? userId,
        string? pathContains,
        string? errorCode,
        int? minStatusCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportAiUsageExcelAsync(
        int? userId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportAuditLogsExcelAsync(
        int? userId,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportSystemErrorsExcelAsync(
        int? userId,
        string? pathContains,
        string? errorCode,
        int? minStatusCode,
        CancellationToken cancellationToken = default);
}
