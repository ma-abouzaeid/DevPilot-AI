using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Admin;

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService? _auditService;

    public AdminService(IApplicationDbContext db, IAuditService? auditService = null)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<AdminOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync(cancellationToken);
        var activeUsers = await _db.Users.CountAsync(cancellationToken);
        var deletedUsers = totalUsers - activeUsers;
        var adminUsers = await _db.UserRoles
            .IgnoreQueryFilters()
            .Include(ur => ur.Role)
            .CountAsync(ur => ur.Role.Name == AppRoles.Admin, cancellationToken);

        var publishedArticles = await _db.KnowledgeArticles.CountAsync(a => a.IsPublished, cancellationToken);
        var unpublishedArticles = await _db.KnowledgeArticles.CountAsync(a => !a.IsPublished, cancellationToken);
        var openProblems = await _db.Problems.CountAsync(
            p => p.Status == ProblemStatus.Open || p.Status == ProblemStatus.InProgress,
            cancellationToken);
        var activeSubscriptions = await _db.UserSubscriptions.CountAsync(s => s.Status == "Active", cancellationToken);

        var aiToday = await _db.AIUsageEvents.CountAsync(e => e.CreatedAt >= today && e.CreatedAt < tomorrow, cancellationToken);
        var creditsToday = await _db.UsageLedgers
            .Where(l => l.CreatedAt >= today && l.CreatedAt < tomorrow && l.EntryType == "CHARGE")
            .SumAsync(l => (int?)l.Amount, cancellationToken) ?? 0;

        return new AdminOverviewDto(
            totalUsers,
            activeUsers,
            deletedUsers,
            adminUsers,
            publishedArticles,
            unpublishedArticles,
            aiToday,
            creditsToday,
            openProblems,
            activeSubscriptions);
    }

    public async Task<PagedResultDto<AdminUserListItemDto>> ListUsersAsync(
        string? search,
        bool includeDeleted,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var query = _db.Users.IgnoreQueryFilters().AsNoTracking().AsQueryable();
        if (!includeDeleted)
        {
            query = query.Where(u => !u.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Email.ToLower().Contains(term) ||
                u.Name.ToLower().Contains(term) ||
                u.UserId.ToString() == term);
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.UserId).ToList();
        var plans = await GetActivePlanCodesAsync(userIds, cancellationToken);
        var credits = await GetRemainingCreditsAsync(userIds, cancellationToken);

        var items = users.Select(u =>
        {
            var hasPlan = plans.TryGetValue(u.UserId, out var plan);
            credits.TryGetValue(u.UserId, out var remaining);
            return new AdminUserListItemDto(
                u.UserId,
                u.Name,
                u.Email,
                u.EmailVerifiedAt,
                u.IsDeleted,
                u.DeletedAt,
                u.CreatedAt,
                GetRoleNames(u),
                hasPlan ? plan.Code : null,
                hasPlan ? plan.Name : null,
                remaining);
        }).ToList();

        return new PagedResultDto<AdminUserListItemDto>(items, total, page, pageSize);
    }

    public async Task<AdminUserDetailDto> GetUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User {userId} was not found.");

        var planMap = await GetActivePlanCodesAsync(new[] { userId }, cancellationToken);
        var hasPlan = planMap.TryGetValue(userId, out var plan);
        var period = await _db.UsagePeriods.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

        var remaining = period == null
            ? 0
            : Math.Max(0, period.CreditsAllocated - period.CreditsUsed + period.CreditsRefunded);

        return new AdminUserDetailDto(
            user.UserId,
            user.Name,
            user.Email,
            user.EmailVerifiedAt,
            user.IsDeleted,
            user.DeletedAt,
            user.CreatedAt,
            GetRoleNames(user),
            hasPlan ? plan.Code : null,
            hasPlan ? plan.Name : null,
            period?.CreditsAllocated ?? 0,
            period?.CreditsUsed ?? 0,
            remaining,
            period?.PeriodStart,
            period?.PeriodEnd);
    }

    public async Task DeactivateUserAsync(int adminUserId, int userId, CancellationToken cancellationToken = default)
    {
        if (adminUserId == userId)
        {
            throw AppError.InvalidRequest("You cannot deactivate your own admin account.", "CANNOT_DEACTIVATE_SELF");
        }

        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User {userId} was not found.");

        if (user.IsDeleted) return;

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;

        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(
            SystemAuditEventTypes.UserDeactivated,
            nameof(User),
            userId.ToString(),
            adminUserId,
            new { TargetUserId = userId },
            cancellationToken);
    }

    public async Task RestoreUserAsync(int adminUserId, int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User {userId} was not found.");

        if (!user.IsDeleted) return;

        user.IsDeleted = false;
        user.DeletedAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            SystemAuditEventTypes.UserRestored,
            nameof(User),
            userId.ToString(),
            adminUserId,
            new { TargetUserId = userId },
            cancellationToken);
    }

    public async Task AssignUserPlanAsync(
        int adminUserId,
        int userId,
        AssignUserPlanRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.UserId == userId, cancellationToken);
        if (!userExists) throw AppError.NotFound($"User {userId} was not found.");

        var code = request.PlanCode.Trim().ToUpperInvariant();
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == code && p.Status == "Active", cancellationToken)
            ?? throw AppError.NotFound($"Plan '{code}' was not found or is inactive.");

        var active = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "Active")
            .ToListAsync(cancellationToken);

        foreach (var sub in active)
        {
            if (sub.PlanId == plan.PlanId)
            {
                await EnsurePeriodForPlanAsync(userId, plan, cancellationToken);
                return;
            }

            sub.Status = "Cancelled";
            sub.CancelledAt = DateTime.UtcNow;
            sub.EndDate = DateTime.UtcNow;
            sub.CancellationReason = $"Admin switched to {plan.Code}";
            sub.UpdatedAt = DateTime.UtcNow;
        }

        _db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = plan.PlanId,
            Status = "Active",
            StartDate = DateTime.UtcNow,
            AssignedByUserId = adminUserId,
            AssignmentSource = "Admin",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        await EnsurePeriodForPlanAsync(userId, plan, cancellationToken);

        await AuditAsync(
            SystemAuditEventTypes.UserPlanAssigned,
            nameof(UserSubscription),
            userId.ToString(),
            adminUserId,
            new { TargetUserId = userId, PlanCode = plan.Code, PlanId = plan.PlanId },
            cancellationToken);
    }

    public async Task SetUserAdminAsync(
        int adminUserId,
        int userId,
        SetUserAdminRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == userId && !request.IsAdmin)
        {
            throw AppError.InvalidRequest("You cannot revoke your own Admin role.", "CANNOT_REVOKE_SELF");
        }

        var user = await _db.Users.IgnoreQueryFilters()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User {userId} was not found.");

        var adminRole = await EnsureRoleAsync(AppRoles.Admin, cancellationToken);
        var existing = user.UserRoles.FirstOrDefault(ur => ur.RoleId == adminRole.RoleId);

        if (request.IsAdmin)
        {
            if (existing == null)
            {
                _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = adminRole.RoleId });
                await _db.SaveChangesAsync(cancellationToken);
                await AuditAsync(
                    SystemAuditEventTypes.AdminRoleGranted,
                    nameof(UserRole),
                    userId.ToString(),
                    adminUserId,
                    new { TargetUserId = userId, Role = AppRoles.Admin },
                    cancellationToken);
            }
        }
        else if (existing != null)
        {
            _db.UserRoles.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(
                SystemAuditEventTypes.AdminRoleRevoked,
                nameof(UserRole),
                userId.ToString(),
                adminUserId,
                new { TargetUserId = userId, Role = AppRoles.Admin },
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<AdminPlanDto>> ListPlansAsync(CancellationToken cancellationToken = default)
    {
        var plans = await _db.Plans.AsNoTracking()
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .OrderBy(p => p.PlanId)
            .ToListAsync(cancellationToken);

        return plans.Select(ToPlanDto).ToList();
    }

    public async Task<AdminPlanDto> UpdatePlanAsync(
        int adminUserId,
        int planId,
        UpdateAdminPlanRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var plan = await _db.Plans
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .FirstOrDefaultAsync(p => p.PlanId == planId, cancellationToken)
            ?? throw AppError.NotFound($"Plan {planId} was not found.");

        var old = new { plan.Name, plan.Description, plan.Status, plan.IsPublic, plan.CreditsPerPeriod, plan.Price, plan.Currency };

        plan.Name = request.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.Status = request.Status.Trim();
        plan.IsPublic = request.IsPublic;
        plan.CreditsPerPeriod = request.CreditsPerPeriod;
        plan.Price = request.Price;
        plan.Currency = string.IsNullOrWhiteSpace(request.Currency) ? plan.Currency : request.Currency.Trim().ToUpperInvariant();
        plan.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(
            SystemAuditEventTypes.PlanUpdated,
            nameof(Plan),
            planId.ToString(),
            adminUserId,
            new { Old = old, New = new { plan.Name, plan.Description, plan.Status, plan.IsPublic, plan.CreditsPerPeriod, plan.Price, plan.Currency } },
            cancellationToken);

        return ToPlanDto(plan);
    }

    public async Task<PagedResultDto<AdminArticleListItemDto>> ListArticlesAsync(
        string? search,
        bool? isPublished,
        bool includeDeleted,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var query = _db.KnowledgeArticles.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Problem)
            .AsQueryable();

        if (!includeDeleted) query = query.Where(a => !a.IsDeleted);
        if (isPublished.HasValue) query = query.Where(a => a.IsPublished == isPublished.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.Title.ToLower().Contains(term) ||
                a.ArticleCode.ToLower().Contains(term) ||
                a.Summary.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var articles = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var authorIds = articles.Select(a => a.Problem.UserId).Distinct().ToList();
        var authors = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => authorIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Name, cancellationToken);

        var items = articles.Select(a => new AdminArticleListItemDto(
            a.KnowledgeArticleId,
            a.ArticleCode,
            a.Title,
            Truncate(a.Summary, 180),
            a.IsPublished,
            a.IsDeleted,
            a.CurrentVersion,
            a.CreatedAt,
            a.ProblemId,
            a.Problem?.ProblemCode,
            a.Problem?.UserId,
            a.Problem != null && authors.TryGetValue(a.Problem.UserId, out var name) ? name : null
        )).ToList();

        return new PagedResultDto<AdminArticleListItemDto>(items, total, page, pageSize);
    }

    public async Task<AdminArticleDetailDto> GetArticleAsync(int articleId, CancellationToken cancellationToken = default)
    {
        var article = await _db.KnowledgeArticles.IgnoreQueryFilters()
            .Include(a => a.Problem)
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.KnowledgeArticleId == articleId, cancellationToken)
            ?? throw AppError.NotFound($"Article {articleId} was not found.");

        var version = article.Versions
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        return new AdminArticleDetailDto(
            article.KnowledgeArticleId,
            article.ArticleCode,
            article.Title,
            article.Summary,
            version?.ContentMarkdown ?? string.Empty,
            article.IsPublished,
            article.IsDeleted,
            article.CurrentVersion,
            article.CreatedAt,
            article.ProblemId,
            article.Problem?.ProblemCode);
    }

    public async Task<AdminArticleDetailDto> UpdateArticleAsync(
        int adminUserId,
        int articleId,
        UpdateAdminArticleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var article = await _db.KnowledgeArticles.IgnoreQueryFilters()
            .Include(a => a.Problem)
            .Include(a => a.Versions)
            .FirstOrDefaultAsync(a => a.KnowledgeArticleId == articleId, cancellationToken)
            ?? throw AppError.NotFound($"Article {articleId} was not found.");

        if (article.IsDeleted)
        {
            throw AppError.Conflict("Restore the article before editing it.", "ARTICLE_DELETED");
        }

        article.Title = request.Title.Trim();
        article.Summary = request.Summary.Trim();
        article.CurrentVersion += 1;

        _db.ArticleVersions.Add(new ArticleVersion
        {
            KnowledgeArticleId = article.KnowledgeArticleId,
            VersionNumber = article.CurrentVersion,
            Title = article.Title,
            ContentMarkdown = request.ContentMarkdown,
            ChangeReason = string.IsNullOrWhiteSpace(request.ChangeReason) ? "Admin edit" : request.ChangeReason.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(
            SystemAuditEventTypes.ArticleUpdated,
            nameof(KnowledgeArticle),
            articleId.ToString(),
            adminUserId,
            new { article.Title, article.CurrentVersion },
            cancellationToken);

        return await GetArticleAsync(articleId, cancellationToken);
    }

    public async Task SetArticlePublishedAsync(
        int adminUserId,
        int articleId,
        bool published,
        CancellationToken cancellationToken = default)
    {
        var article = await _db.KnowledgeArticles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.KnowledgeArticleId == articleId, cancellationToken)
            ?? throw AppError.NotFound($"Article {articleId} was not found.");

        if (article.IsDeleted)
        {
            throw AppError.Conflict("Restore the article before changing publish state.", "ARTICLE_DELETED");
        }

        article.IsPublished = published;
        await _db.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            published ? SystemAuditEventTypes.ArticlePublished : SystemAuditEventTypes.ArticleUnpublished,
            nameof(KnowledgeArticle),
            articleId.ToString(),
            adminUserId,
            new { IsPublished = published },
            cancellationToken);
    }

    public async Task SoftDeleteArticleAsync(int adminUserId, int articleId, CancellationToken cancellationToken = default)
    {
        var article = await _db.KnowledgeArticles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.KnowledgeArticleId == articleId, cancellationToken)
            ?? throw AppError.NotFound($"Article {articleId} was not found.");

        if (article.IsDeleted) return;

        article.IsDeleted = true;
        article.DeletedAt = DateTime.UtcNow;
        article.IsPublished = false;
        await _db.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            SystemAuditEventTypes.ArticleDeleted,
            nameof(KnowledgeArticle),
            articleId.ToString(),
            adminUserId,
            new { SoftDeleted = true },
            cancellationToken);
    }

    public async Task RestoreArticleAsync(int adminUserId, int articleId, CancellationToken cancellationToken = default)
    {
        var article = await _db.KnowledgeArticles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.KnowledgeArticleId == articleId, cancellationToken)
            ?? throw AppError.NotFound($"Article {articleId} was not found.");

        if (!article.IsDeleted) return;

        article.IsDeleted = false;
        article.DeletedAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            SystemAuditEventTypes.ArticleRestored,
            nameof(KnowledgeArticle),
            articleId.ToString(),
            adminUserId,
            new { Restored = true },
            cancellationToken);
    }

    public async Task<PagedResultDto<AdminAuditLogDto>> ListAuditLogsAsync(
        int? userId,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(action))
        {
            var act = action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action.ToUpper().Contains(act));
        }
        if (fromUtc.HasValue) query = query.Where(a => a.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(a => a.CreatedAt <= toUtc.Value);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var emails = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Email, cancellationToken);

        var items = rows.Select(r => new AdminAuditLogDto(
            r.AuditLogId,
            r.UserId,
            r.UserId.HasValue && emails.TryGetValue(r.UserId.Value, out var email) ? email : null,
            r.Action,
            r.EntityName,
            r.EntityId,
            r.OldValues,
            r.NewValues,
            r.IpAddress,
            r.RequestId,
            r.CreatedAt
        )).ToList();

        return new PagedResultDto<AdminAuditLogDto>(items, total, page, pageSize);
    }

    public async Task<PagedResultDto<AdminAiUsageDto>> ListAiUsageAsync(
        int? userId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var query = _db.AIUsageEvents.AsNoTracking().AsQueryable();

        if (userId.HasValue) query = query.Where(e => e.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(e => e.Status == st);
        }
        if (fromUtc.HasValue) query = query.Where(e => e.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(e => e.CreatedAt <= toUtc.Value);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.UserId).Distinct().ToList();
        var emails = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Email, cancellationToken);

        var items = rows.Select(e => new AdminAiUsageDto(
            e.AIUsageEventId,
            e.AIRequestId,
            e.UserId,
            emails.TryGetValue(e.UserId, out var email) ? email : null,
            e.ProblemId,
            e.OperationType,
            e.Status,
            e.InputTokens,
            e.OutputTokens,
            e.TotalTokens,
            e.CreditsCharged,
            e.EstimatedCost,
            e.CorrelationId,
            e.CreatedAt,
            e.ErrorCode
        )).ToList();

        return new PagedResultDto<AdminAiUsageDto>(items, total, page, pageSize);
    }

    private async Task EnsurePeriodForPlanAsync(int userId, Plan plan, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var period = await _db.UsagePeriods
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PeriodStart == start && p.PeriodEnd == end, cancellationToken);

        if (period == null)
        {
            _db.UsagePeriods.Add(new UsagePeriod
            {
                UserId = userId,
                PlanId = plan.PlanId,
                PeriodStart = start,
                PeriodEnd = end,
                CreditsAllocated = plan.CreditsPerPeriod,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            period.PlanId = plan.PlanId;
            period.CreditsAllocated = plan.CreditsPerPeriod;
            period.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Role> EnsureRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role != null) return role;

        role = new Role { Name = roleName };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        return role;
    }

    private async Task<Dictionary<int, (string Code, string Name)>> GetActivePlanCodesAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0) return new Dictionary<int, (string, string)>();

        var subs = await _db.UserSubscriptions.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId) && s.Status == "Active")
            .Include(s => s.Plan)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(cancellationToken);

        return subs
            .GroupBy(s => s.UserId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var s = g.First();
                    return (s.Plan.Code, s.Plan.Name);
                });
    }

    private async Task<Dictionary<int, int>> GetRemainingCreditsAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var periods = await _db.UsagePeriods.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId) && p.PeriodStart == start && p.PeriodEnd == end)
            .ToListAsync(cancellationToken);

        return periods.ToDictionary(
            p => p.UserId,
            p => Math.Max(0, p.CreditsAllocated - p.CreditsUsed + p.CreditsRefunded));
    }

    private async Task AuditAsync(
        string action,
        string entityName,
        string entityId,
        int adminUserId,
        object newValues,
        CancellationToken cancellationToken)
    {
        if (_auditService == null) return;
        await _auditService.LogAsync(
            action,
            entityName,
            entityId,
            oldValues: null,
            newValues: newValues,
            userId: adminUserId,
            cancellationToken: cancellationToken);
    }

    private static AdminPlanDto ToPlanDto(Plan plan) =>
        new(
            plan.PlanId,
            plan.Code,
            plan.Name,
            plan.Description,
            plan.Status,
            plan.IsDefault,
            plan.IsPublic,
            plan.PeriodType,
            plan.CreditsPerPeriod,
            plan.Price,
            plan.Currency,
            plan.PlanFeatures
                .Where(pf => pf.IsEnabled)
                .Select(pf => pf.Feature.Key)
                .OrderBy(k => k)
                .ToList());

    private static IReadOnlyList<string> GetRoleNames(User user) =>
        user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;
        return (page, pageSize);
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }
}
