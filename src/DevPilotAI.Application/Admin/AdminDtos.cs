using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Admin;

public record AdminOverviewDto(
    int TotalUsers,
    int ActiveUsers,
    int DeletedUsers,
    int AdminUsers,
    int PublishedArticles,
    int UnpublishedArticles,
    int AiRequestsToday,
    int CreditsChargedToday,
    int OpenProblems,
    int ActiveSubscriptions
);

public record AdminUserListItemDto(
    int UserId,
    string Name,
    string Email,
    DateTime? EmailVerifiedAt,
    bool IsDeleted,
    DateTime? DeletedAt,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles,
    string? PlanCode,
    string? PlanName,
    int CreditsRemaining,
    bool HasUnlimitedCredits = false
);

public record AdminUserDetailDto(
    int UserId,
    string Name,
    string Email,
    DateTime? EmailVerifiedAt,
    bool IsDeleted,
    DateTime? DeletedAt,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles,
    string? PlanCode,
    string? PlanName,
    int CreditsAllocated,
    int CreditsUsed,
    int CreditsRemaining,
    DateTime? PeriodStart,
    DateTime? PeriodEnd,
    bool HasUnlimitedCredits = false
);

public record AssignUserPlanRequestDto(
    [Required]
    [StringLength(50, MinimumLength = 1)]
    string PlanCode
);

public record SetUserAdminRequestDto(
    [Required]
    bool IsAdmin
);

public record SetUserUnlimitedCreditsRequestDto(
    [Required]
    bool HasUnlimitedCredits
);

public class CreateAdminUserRequestDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public string Password { get; set; } = string.Empty;

    [StringLength(50)]
    public string? PlanCode { get; set; }

    public bool IsAdmin { get; set; }

    public bool MarkEmailVerified { get; set; } = true;
}

public class UpdateAdminUserRequestDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).*$", ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, and one number.")]
    public string? NewPassword { get; set; }

    public bool? MarkEmailVerified { get; set; }
}

public record AdminPlanDto(
    int PlanId,
    string Code,
    string Name,
    string? Description,
    string Status,
    bool IsDefault,
    bool IsPublic,
    string PeriodType,
    int CreditsPerPeriod,
    decimal? Price,
    string? Currency,
    IReadOnlyList<string> Features,
    bool SaleEnabled = false,
    decimal SaleDiscountPercent = 0,
    decimal? EffectivePrice = null
);

public class CreateAdminPlanRequestDto
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Active";

    public bool IsPublic { get; set; } = true;

    public bool IsDefault { get; set; }

    [Range(0, 1_000_000)]
    public int CreditsPerPeriod { get; set; } = 100;

    [Range(0, 1_000_000)]
    public decimal? Price { get; set; }

    [StringLength(10)]
    public string? Currency { get; set; } = "USD";

    public bool EnableCodeReview { get; set; }

    public bool SaleEnabled { get; set; }

    [Range(0, 100)]
    public decimal SaleDiscountPercent { get; set; }
}

public class UpdateAdminPlanRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Active";

    public bool IsPublic { get; set; } = true;

    public bool IsDefault { get; set; }

    [Range(0, 1_000_000)]
    public int CreditsPerPeriod { get; set; }

    [Range(0, 1_000_000)]
    public decimal? Price { get; set; }

    [StringLength(10)]
    public string? Currency { get; set; }

    public bool? EnableCodeReview { get; set; }

    public bool SaleEnabled { get; set; }

    [Range(0, 100)]
    public decimal SaleDiscountPercent { get; set; }
}

public record AdminArticleListItemDto(
    int KnowledgeArticleId,
    string ArticleCode,
    string Title,
    string Summary,
    bool IsPublished,
    bool IsDeleted,
    int CurrentVersion,
    DateTime CreatedAt,
    int ProblemId,
    string? ProblemCode,
    int? AuthorUserId,
    string? AuthorName
);

public record AdminArticleDetailDto(
    int KnowledgeArticleId,
    string ArticleCode,
    string Title,
    string Summary,
    string ContentMarkdown,
    bool IsPublished,
    bool IsDeleted,
    int CurrentVersion,
    DateTime CreatedAt,
    int ProblemId,
    string? ProblemCode
);

public class UpdateAdminArticleRequestDto
{
    [Required]
    [StringLength(250, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    [StringLength(200_000, MinimumLength = 1)]
    public string ContentMarkdown { get; set; } = string.Empty;

    [StringLength(250)]
    public string? ChangeReason { get; set; }
}

public record AdminAuditLogDto(
    long AuditLogId,
    int? UserId,
    string? UserEmail,
    string Action,
    string EntityName,
    string EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? UserAgent,
    string? RequestId,
    DateTime CreatedAt,
    string DetailsJson
);

public record AdminAiUsageDto(
    long AIUsageEventId,
    int AIRequestId,
    int UserId,
    string? UserEmail,
    int? ProblemId,
    string OperationType,
    string Status,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    int? CreditsCharged,
    decimal? EstimatedCost,
    string? CorrelationId,
    DateTime CreatedAt,
    string? ErrorCode,
    string? ProviderName,
    string? ModelName,
    string? ErrorMessage,
    long? DurationMs,
    int RetrySequence,
    string DetailsJson
);

public record AdminSystemErrorDto(
    long SystemErrorLogId,
    string TraceId,
    string HttpMethod,
    string Path,
    string? QueryString,
    string Action,
    int StatusCode,
    string ErrorCode,
    string Message,
    string? ExceptionType,
    string? ExceptionMessage,
    string? StackTrace,
    string? ResponseJson,
    int? UserId,
    string? UserEmail,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt,
    string DetailsJson
);
