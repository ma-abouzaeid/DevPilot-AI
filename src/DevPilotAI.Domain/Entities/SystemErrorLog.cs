namespace DevPilotAI.Domain.Entities;

/// <summary>API errors for the admin error log.</summary>
public class SystemErrorLog
{
    public long SystemErrorLogId { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string Action { get; set; } = "HTTP_REQUEST";
    public int StatusCode { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? ResponseJson { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
