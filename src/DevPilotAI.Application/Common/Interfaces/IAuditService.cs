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
