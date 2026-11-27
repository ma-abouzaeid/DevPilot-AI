using System.Text.Json;
using DevPilotAI.Application.Common;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Plans;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Admin;

public class AdminService : IAdminService
{
    private readonly IApplicationDbContext _db;
    private readonly IAuditService? _auditService;
    private readonly PasswordHasher<User> _passwordHasher = new();

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
                remaining,
                u.HasUnlimitedCredits);
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
            period?.PeriodEnd,
            user.HasUnlimitedCredits);
    }

    public async Task<AdminUserDetailDto> CreateUserAsync(
        int adminUserId,
        CreateAdminUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var name = request.Name.Trim();

        var exists = await _db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email.ToLower() == email && !u.IsDeleted, cancellationToken);
        if (exists)
        {
            throw AppError.Conflict("An account with this email already exists.", "EMAIL_ALREADY_EXISTS");
        }

        var user = new User
        {
            Name = name,
            Email = email,
            PasswordHash = string.Empty,
            EmailVerifiedAt = request.MarkEmailVerified ? DateTime.UtcNow : null,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var developerRole = await EnsureRoleAsync(AppRoles.Developer, cancellationToken);
        _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = developerRole.RoleId });

        if (request.IsAdmin)
        {
            var adminRole = await EnsureRoleAsync(AppRoles.Admin, cancellationToken);
            _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = adminRole.RoleId });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var planCode = string.IsNullOrWhiteSpace(request.PlanCode) ? null : request.PlanCode.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(planCode))
        {
            await AssignUserPlanAsync(adminUserId, user.UserId, new AssignUserPlanRequestDto(planCode), cancellationToken);
        }
        else
        {
            var defaultPlan = await _db.Plans.FirstOrDefaultAsync(p => p.IsDefault && p.Status == "Active", cancellationToken);
            if (defaultPlan != null)
            {
                await AssignUserPlanAsync(adminUserId, user.UserId, new AssignUserPlanRequestDto(defaultPlan.Code), cancellationToken);
            }
        }

        await AuditAsync(
            SystemAuditEventTypes.UserCreatedByAdmin,
            nameof(User),
            user.UserId.ToString(),
            adminUserId,
            new { user.UserId, user.Email, user.Name, request.IsAdmin, PlanCode = planCode },
            cancellationToken);

        return await GetUserAsync(user.UserId, cancellationToken);
    }

    public async Task<AdminUserDetailDto> UpdateUserAsync(
        int adminUserId,
        int userId,
        UpdateAdminUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User {userId} was not found.");

        var name = request.Name.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        var old = new { user.Name, user.Email, user.EmailVerifiedAt };

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await _db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.UserId != userId && u.Email.ToLower() == email && !u.IsDeleted, cancellationToken);
            if (taken)
            {
                throw AppError.Conflict("This email is already associated with another account.", "EMAIL_ALREADY_IN_USE");
            }
        }

        user.Name = name;
        user.Email = email;

        if (request.MarkEmailVerified == true && user.EmailVerifiedAt == null)
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
        }
        else if (request.MarkEmailVerified == false)
        {
            user.EmailVerifiedAt = null;
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
            var tokens = await _db.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync(cancellationToken);
            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(
            SystemAuditEventTypes.UserUpdatedByAdmin,
            nameof(User),
            userId.ToString(),
            adminUserId,
            new { Old = old, New = new { user.Name, user.Email, user.EmailVerifiedAt, PasswordChanged = !string.IsNullOrWhiteSpace(request.NewPassword) } },
            cancellationToken);

        return await GetUserAsync(userId, cancellationToken);
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

        var now = DateTime.UtcNow;
        foreach (var sub in active)
        {
            if (sub.PlanId == plan.PlanId)
            {
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

                await EnsurePeriodForPlanAsync(userId, plan, cancellationToken);
                return;
            }

            sub.Status = "Cancelled";
            sub.CancelledAt = now;
            sub.EndDate = now;
            sub.CancellationReason = $"Admin switched to {plan.Code}";
            sub.UpdatedAt = now;
        }

        _db.UserSubscriptions.Add(new UserSubscription
        {
            UserId = userId,
            PlanId = plan.PlanId,
            Status = "Active",
            StartDate = now,
            EndDate = plan.ComputeSubscriptionEndDate(now),
            AssignedByUserId = adminUserId,
            AssignmentSource = "Admin",
            CreatedAt = now,
            UpdatedAt = now
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

    public async Task SetUserUnlimitedCreditsAsync(
        int adminUserId,
        int userId,
        SetUserUnlimitedCreditsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
            ?? throw AppError.NotFound($"User {userId} was not found.");

        if (user.HasUnlimitedCredits == request.HasUnlimitedCredits)
        {
            return;
        }

        user.HasUnlimitedCredits = request.HasUnlimitedCredits;
        await _db.SaveChangesAsync(cancellationToken);

        await AuditAsync(
            request.HasUnlimitedCredits
                ? SystemAuditEventTypes.UnlimitedCreditsGranted
                : SystemAuditEventTypes.UnlimitedCreditsRevoked,
            nameof(User),
            userId.ToString(),
            adminUserId,
            new { TargetUserId = userId, HasUnlimitedCredits = request.HasUnlimitedCredits },
            cancellationToken);
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

    public async Task<AdminPlanDto> CreatePlanAsync(
        int adminUserId,
        CreateAdminPlanRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Plans.AnyAsync(p => p.Code == code, cancellationToken))
        {
            throw AppError.Conflict($"Plan code '{code}' already exists.", "PLAN_CODE_EXISTS");
        }

        if (request.IsDefault)
        {
            await ClearDefaultPlansAsync(cancellationToken);
        }

        var plan = new Plan
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            IsPublic = request.IsPublic,
            IsDefault = request.IsDefault,
            PeriodType = "Monthly",
            CreditsPerPeriod = request.CreditsPerPeriod,
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
            SaleEnabled = request.SaleEnabled,
            SaleDiscountPercent = NormalizeSalePercent(request.SaleEnabled, request.SaleDiscountPercent),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);

        await EnsurePlanFeaturesAsync(plan.PlanId, request.CreditsPerPeriod, request.EnableCodeReview, cancellationToken);

        await AuditAsync(
            SystemAuditEventTypes.PlanCreated,
            nameof(Plan),
            plan.PlanId.ToString(),
            adminUserId,
            new { plan.Code, plan.Name, plan.CreditsPerPeriod, plan.IsDefault, request.EnableCodeReview, plan.SaleEnabled, plan.SaleDiscountPercent },
            cancellationToken);

        return (await ListPlansAsync(cancellationToken)).First(p => p.PlanId == plan.PlanId);
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

        var old = new { plan.Name, plan.Description, plan.Status, plan.IsPublic, plan.IsDefault, plan.CreditsPerPeriod, plan.Price, plan.Currency, plan.SaleEnabled, plan.SaleDiscountPercent };

        if (request.IsDefault && !plan.IsDefault)
        {
            await ClearDefaultPlansAsync(cancellationToken);
        }

        plan.Name = request.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.Status = request.Status.Trim();
        plan.IsPublic = request.IsPublic;
        plan.IsDefault = request.IsDefault;
        plan.CreditsPerPeriod = request.CreditsPerPeriod;
        plan.Price = request.Price;
        plan.Currency = string.IsNullOrWhiteSpace(request.Currency) ? plan.Currency : request.Currency.Trim().ToUpperInvariant();
        plan.SaleEnabled = request.SaleEnabled;
        plan.SaleDiscountPercent = NormalizeSalePercent(request.SaleEnabled, request.SaleDiscountPercent);
        plan.UpdatedAt = DateTime.UtcNow;

        if (request.EnableCodeReview.HasValue)
        {
            await EnsurePlanFeaturesAsync(plan.PlanId, request.CreditsPerPeriod, request.EnableCodeReview.Value, cancellationToken);
        }
        else
        {
            // Sync credit limit with allocation.
            var creditsFeature = await _db.Features.FirstOrDefaultAsync(f => f.Key == PlanFeatureKeys.MonthlyAiCredits, cancellationToken);
            if (creditsFeature != null)
            {
                var pf = await _db.PlanFeatures.FirstOrDefaultAsync(
                    x => x.PlanId == plan.PlanId && x.FeatureId == creditsFeature.FeatureId,
                    cancellationToken);
                if (pf != null)
                {
                    pf.LimitValue = request.CreditsPerPeriod;
                    pf.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Reload features.
        plan = await _db.Plans
            .Include(p => p.PlanFeatures)
                .ThenInclude(pf => pf.Feature)
            .FirstAsync(p => p.PlanId == planId, cancellationToken);

        await AuditAsync(
            SystemAuditEventTypes.PlanUpdated,
            nameof(Plan),
            planId.ToString(),
            adminUserId,
            new { Old = old, New = new { plan.Name, plan.Description, plan.Status, plan.IsPublic, plan.IsDefault, plan.CreditsPerPeriod, plan.Price, plan.Currency, plan.SaleEnabled, plan.SaleDiscountPercent, request.EnableCodeReview } },
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

        var items = rows.Select(r =>
        {
            var details = BuildJson(new
            {
                r.AuditLogId,
                r.Action,
                r.EntityName,
                r.EntityId,
                r.UserId,
                UserEmail = r.UserId.HasValue && emails.TryGetValue(r.UserId.Value, out var email) ? email : null,
                r.OldValues,
                r.NewValues,
                ParsedOldValues = TryParseJson(r.OldValues),
                ParsedNewValues = TryParseJson(r.NewValues),
                r.IpAddress,
                r.UserAgent,
                r.RequestId,
                r.CreatedAt
            });

            return new AdminAuditLogDto(
                r.AuditLogId,
                r.UserId,
                r.UserId.HasValue && emails.TryGetValue(r.UserId.Value, out var mail) ? mail : null,
                r.Action,
                r.EntityName,
                r.EntityId,
                r.OldValues,
                r.NewValues,
                r.IpAddress,
                r.UserAgent,
                r.RequestId,
                r.CreatedAt,
                details
            );
        }).ToList();

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

        var providerIds = rows.Select(r => r.ProviderId).Distinct().ToList();
        var modelIds = rows.Where(r => r.ModelId.HasValue).Select(r => r.ModelId!.Value).Distinct().ToList();
        var requestIds = rows.Select(r => r.AIRequestId).Distinct().ToList();

        var providers = await _db.AIProviders.AsNoTracking()
            .Where(p => providerIds.Contains(p.AIProviderId))
            .ToDictionaryAsync(p => p.AIProviderId, p => p.Name, cancellationToken);

        var models = await _db.AIModels.AsNoTracking()
            .Where(m => modelIds.Contains(m.AIModelId))
            .ToDictionaryAsync(m => m.AIModelId, m => m.ModelName, cancellationToken);

        var requests = await _db.AIRequests.AsNoTracking()
            .Where(r => requestIds.Contains(r.AIRequestId))
            .ToDictionaryAsync(r => r.AIRequestId, cancellationToken);

        // Fill in provider/model from AIRequest when usage row ids are missing.
        var requestProviderIds = requests.Values.Select(r => r.AIProviderId).Distinct().ToList();
        var requestModelIds = requests.Values.Select(r => r.AIModelId).Distinct().ToList();
        var missingProviderIds = requestProviderIds.Where(id => !providers.ContainsKey(id)).ToList();
        if (missingProviderIds.Count > 0)
        {
            var extraProviders = await _db.AIProviders.AsNoTracking()
                .Where(p => missingProviderIds.Contains(p.AIProviderId))
                .ToDictionaryAsync(p => p.AIProviderId, p => p.Name, cancellationToken);
            foreach (var kv in extraProviders) providers[kv.Key] = kv.Value;
        }

        var missingModelIds = requestModelIds.Where(id => !models.ContainsKey(id)).ToList();
        if (missingModelIds.Count > 0)
        {
            var extraModels = await _db.AIModels.AsNoTracking()
                .Where(m => missingModelIds.Contains(m.AIModelId))
                .ToDictionaryAsync(m => m.AIModelId, m => m.ModelName, cancellationToken);
            foreach (var kv in extraModels) models[kv.Key] = kv.Value;
        }

        var items = rows.Select(e =>
        {
            requests.TryGetValue(e.AIRequestId, out var aiRequest);
            var providerId = e.ProviderId > 0 ? e.ProviderId : aiRequest?.AIProviderId ?? 0;
            var modelId = e.ModelId ?? aiRequest?.AIModelId;
            providers.TryGetValue(providerId, out var providerName);
            string? modelName = null;
            if (modelId.HasValue) models.TryGetValue(modelId.Value, out modelName);

            var errorMessage = !string.IsNullOrWhiteSpace(aiRequest?.ErrorMessage)
                ? aiRequest!.ErrorMessage
                : e.ErrorCode;
            // Show names when we have them; otherwise provider/model ids.
            var providerLabel = !string.IsNullOrWhiteSpace(providerName)
                ? providerName
                : (providerId > 0 ? $"Provider #{providerId}" : null);
            var modelLabel = !string.IsNullOrWhiteSpace(modelName)
                ? modelName
                : (modelId.HasValue ? $"Model #{modelId.Value}" : null);
            var details = BuildJson(new
            {
                e.AIUsageEventId,
                e.AIRequestId,
                e.UserId,
                UserEmail = emails.TryGetValue(e.UserId, out var email) ? email : null,
                e.ProblemId,
                e.OperationType,
                e.Status,
                Provider = providerLabel,
                Model = modelLabel,
                ProviderId = providerId,
                ModelId = modelId,
                e.InputTokens,
                e.OutputTokens,
                e.TotalTokens,
                e.CreditsCharged,
                e.CreditsReserved,
                e.CreditsRefunded,
                e.EstimatedCost,
                e.ActualCost,
                e.CostType,
                e.CorrelationId,
                e.IdempotencyKey,
                e.ProviderRequestId,
                e.RetrySequence,
                e.ErrorCode,
                ErrorMessage = errorMessage,
                e.DurationMs,
                e.StartedAt,
                e.CompletedAt,
                e.CreatedAt,
                AIRequest = aiRequest == null ? null : new
                {
                    aiRequest.AIRequestId,
                    aiRequest.TaskType,
                    aiRequest.Success,
                    aiRequest.ErrorMessage,
                    aiRequest.AIProviderId,
                    aiRequest.AIModelId,
                    Provider = providers.TryGetValue(aiRequest.AIProviderId, out var pName) ? pName : null,
                    Model = models.TryGetValue(aiRequest.AIModelId, out var mName) ? mName : null,
                    aiRequest.PromptTokensUsed,
                    aiRequest.CompletionTokensUsed,
                    aiRequest.TotalTokensUsed,
                    aiRequest.DurationMs,
                    aiRequest.CreatedAt
                }
            });

            return new AdminAiUsageDto(
                e.AIUsageEventId,
                e.AIRequestId,
                e.UserId,
                emails.TryGetValue(e.UserId, out var mail) ? mail : null,
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
                e.ErrorCode,
                providerLabel,
                modelLabel,
                errorMessage,
                e.DurationMs,
                e.RetrySequence,
                details
            );
        }).ToList();

        return new PagedResultDto<AdminAiUsageDto>(items, total, page, pageSize);
    }

    public async Task<PagedResultDto<AdminSystemErrorDto>> ListSystemErrorsAsync(
        int? userId,
        string? pathContains,
        string? errorCode,
        int? minStatusCode,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        List<SystemErrorLog> rows;
        int total;
        try
        {
            var query = _db.SystemErrorLogs.AsNoTracking().AsQueryable();

            if (userId.HasValue) query = query.Where(e => e.UserId == userId.Value);
            if (!string.IsNullOrWhiteSpace(pathContains))
            {
                var path = pathContains.Trim();
                query = query.Where(e => e.Path.Contains(path));
            }
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                var code = errorCode.Trim().ToUpperInvariant();
                query = query.Where(e => e.ErrorCode.ToUpper().Contains(code));
            }
            if (minStatusCode.HasValue) query = query.Where(e => e.StatusCode >= minStatusCode.Value);

            total = await query.CountAsync(cancellationToken);
            rows = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Table missing until AddSystemErrorLogs.sql is applied.
            return new PagedResultDto<AdminSystemErrorDto>([], 0, page, pageSize);
        }

        var ids = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var emails = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Email, cancellationToken);

        var items = rows.Select(e =>
        {
            var userEmail = e.UserId.HasValue && emails.TryGetValue(e.UserId.Value, out var mail) ? mail : null;
            var details = BuildJson(new
            {
                e.SystemErrorLogId,
                e.TraceId,
                e.HttpMethod,
                e.Path,
                e.QueryString,
                e.Action,
                e.StatusCode,
                e.ErrorCode,
                e.Message,
                e.ExceptionType,
                e.ExceptionMessage,
                e.StackTrace,
                Response = TryParseJson(e.ResponseJson),
                ResponseJsonRaw = e.ResponseJson,
                e.UserId,
                UserEmail = userEmail,
                e.IpAddress,
                e.UserAgent,
                e.CreatedAt
            });

            return new AdminSystemErrorDto(
                e.SystemErrorLogId,
                e.TraceId,
                e.HttpMethod,
                e.Path,
                e.QueryString,
                e.Action,
                e.StatusCode,
                e.ErrorCode,
                e.Message,
                e.ExceptionType,
                e.ExceptionMessage,
                e.StackTrace,
                e.ResponseJson,
                e.UserId,
                userEmail,
                e.IpAddress,
                e.UserAgent,
                e.CreatedAt,
                details
            );
        }).ToList();

        return new PagedResultDto<AdminSystemErrorDto>(items, total, page, pageSize);
    }

    public async Task<byte[]> ExportAiUsageExcelAsync(
        int? userId,
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var data = await ListAiUsageAsync(userId, status, fromUtc, toUtc, page: 1, pageSize: 5000, cancellationToken);
        var headers = new[]
        {
            "When (UTC)", "User Id", "User Email", "Operation", "Status", "Provider", "Model",
            "Input Tokens", "Output Tokens", "Total Tokens", "Credits", "Estimated Cost",
            "Duration Ms", "Error Code", "Error Message", "Correlation Id", "AI Request Id", "Problem Id"
        };
        var rows = data.Items.Select(e => (IReadOnlyList<object?>)new object?[]
        {
            e.CreatedAt,
            e.UserId,
            e.UserEmail,
            e.OperationType,
            e.Status,
            e.ProviderName,
            e.ModelName,
            e.InputTokens,
            e.OutputTokens,
            e.TotalTokens,
            e.CreditsCharged,
            e.EstimatedCost,
            e.DurationMs,
            e.ErrorCode,
            e.ErrorMessage,
            e.CorrelationId,
            e.AIRequestId,
            e.ProblemId
        }).ToList();

        return AdminExcelExport.ToXlsx("AI Usage", headers, rows);
    }

    public async Task<byte[]> ExportAuditLogsExcelAsync(
        int? userId,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        var data = await ListAuditLogsAsync(userId, action, fromUtc, toUtc, page: 1, pageSize: 5000, cancellationToken);
        var headers = new[]
        {
            "When (UTC)", "Action", "User Id", "User Email", "Entity", "Entity Id",
            "Old Values", "New Values", "IP", "User Agent", "Request Id"
        };
        var rows = data.Items.Select(e => (IReadOnlyList<object?>)new object?[]
        {
            e.CreatedAt,
            e.Action,
            e.UserId,
            e.UserEmail,
            e.EntityName,
            e.EntityId,
            e.OldValues,
            e.NewValues,
            e.IpAddress,
            e.UserAgent,
            e.RequestId
        }).ToList();

        return AdminExcelExport.ToXlsx("Audit Logs", headers, rows);
    }

    public async Task<byte[]> ExportSystemErrorsExcelAsync(
        int? userId,
        string? pathContains,
        string? errorCode,
        int? minStatusCode,
        CancellationToken cancellationToken = default)
    {
        var data = await ListSystemErrorsAsync(
            userId, pathContains, errorCode, minStatusCode, page: 1, pageSize: 5000, cancellationToken);
        var headers = new[]
        {
            "When (UTC)", "Status", "Error Code", "Method", "Path", "Action", "Message",
            "User Id", "User Email", "Exception Type", "Exception Message", "Trace Id", "IP"
        };
        var rows = data.Items.Select(e => (IReadOnlyList<object?>)new object?[]
        {
            e.CreatedAt,
            e.StatusCode,
            e.ErrorCode,
            e.HttpMethod,
            e.Path,
            e.Action,
            e.Message,
            e.UserId,
            e.UserEmail,
            e.ExceptionType,
            e.ExceptionMessage,
            e.TraceId,
            e.IpAddress
        }).ToList();

        return AdminExcelExport.ToXlsx("System Errors", headers, rows);
    }

    private static string BuildJson(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

    private static object? TryParseJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch
        {
            return raw;
        }
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
                CreditsUsed = 0,
                CreditsRefunded = 0,
                CreditsReserved = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
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

    private async Task ClearDefaultPlansAsync(CancellationToken cancellationToken)
    {
        var defaults = await _db.Plans.Where(p => p.IsDefault).ToListAsync(cancellationToken);
        foreach (var p in defaults)
        {
            p.IsDefault = false;
            p.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePlanFeaturesAsync(
        int planId,
        int creditsPerPeriod,
        bool enableCodeReview,
        CancellationToken cancellationToken)
    {
        var features = await _db.Features.ToListAsync(cancellationToken);
        Feature Require(string key) =>
            features.FirstOrDefault(f => f.Key == key)
            ?? throw AppError.NotFound($"Feature '{key}' is not configured.");

        var debugging = Require(PlanFeatureKeys.AiDebugging);
        var credits = Require(PlanFeatureKeys.MonthlyAiCredits);
        var review = Require(PlanFeatureKeys.GitHubCodeReview);

        async Task Upsert(Feature feature, bool enabled, int? limit)
        {
            var existing = await _db.PlanFeatures
                .FirstOrDefaultAsync(pf => pf.PlanId == planId && pf.FeatureId == feature.FeatureId, cancellationToken);
            if (existing == null)
            {
                _db.PlanFeatures.Add(new PlanFeature
                {
                    PlanId = planId,
                    FeatureId = feature.FeatureId,
                    IsEnabled = enabled,
                    LimitValue = limit,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.IsEnabled = enabled;
                existing.LimitValue = limit;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }

        await Upsert(debugging, true, null);
        await Upsert(credits, true, creditsPerPeriod);
        await Upsert(review, enableCodeReview, null);
        await _db.SaveChangesAsync(cancellationToken);
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

    private static decimal NormalizeSalePercent(bool saleEnabled, decimal percent)
    {
        if (!saleEnabled) return 0m;
        if (percent < 0m) return 0m;
        if (percent > 100m) return 100m;
        return Math.Round(percent, 2, MidpointRounding.AwayFromZero);
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
                .ToList(),
            plan.SaleEnabled,
            plan.SaleDiscountPercent,
            plan.GetEffectivePriceUsd());

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
