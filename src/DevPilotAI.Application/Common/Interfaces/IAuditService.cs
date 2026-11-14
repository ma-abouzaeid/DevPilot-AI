namespace DevPilotAI.Application.Common.Interfaces;

public static class SecurityAuditEventTypes
{
    public const string LoginSuccess = "LOGIN_SUCCESS";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string AccountCreated = "ACCOUNT_CREATED";
    public const string EmailVerified = "EMAIL_VERIFIED";
    public const string PasswordReset = "PASSWORD_RESET";
    public const string Logout = "LOGOUT";
    public const string RefreshTokenReuse = "REFRESH_TOKEN_REUSE";
    public const string AccountSoftDeleted = "ACCOUNT_SOFT_DELETED";
    public const string SecurityFailure = "SECURITY_FAILURE";
}

/// <summary>Product/system activity events for admin observability (credits, AI, workflows).</summary>
public static class SystemAuditEventTypes
{
    public const string AiRequestCompleted = "AI_REQUEST_COMPLETED";
    public const string AiRequestFailed = "AI_REQUEST_FAILED";
    public const string CreditsCharged = "CREDITS_CHARGED";
    public const string PlanActivated = "PLAN_ACTIVATED";
    public const string ProblemSubmitted = "PROBLEM_SUBMITTED";
    public const string ProblemFollowUp = "PROBLEM_FOLLOW_UP";
    public const string ProblemSolved = "PROBLEM_SOLVED";
    public const string ChallengeGenerated = "CHALLENGE_GENERATED";
    public const string ChallengeEvaluated = "CHALLENGE_EVALUATED";
    public const string GithubReviewCompleted = "GITHUB_REVIEW_COMPLETED";
    public const string GithubDebugStarted = "GITHUB_DEBUG_STARTED";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string UserRestored = "USER_RESTORED";
    public const string UserPlanAssigned = "USER_PLAN_ASSIGNED";
    public const string PlanUpdated = "PLAN_UPDATED";
    public const string ArticleUpdated = "ARTICLE_UPDATED";
    public const string ArticlePublished = "ARTICLE_PUBLISHED";
    public const string ArticleUnpublished = "ARTICLE_UNPUBLISHED";
    public const string ArticleDeleted = "ARTICLE_DELETED";
    public const string ArticleRestored = "ARTICLE_RESTORED";
    public const string AdminRoleGranted = "ADMIN_ROLE_GRANTED";
    public const string AdminRoleRevoked = "ADMIN_ROLE_REVOKED";
}

public class AuditEvent
{
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public object? OldValues { get; set; }
    public object? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }
}

public interface IAuditService
{
    Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task LogAsync(string action, string entityName, string entityId, object? oldValues = null, object? newValues = null, int? userId = null, CancellationToken cancellationToken = default);
}
