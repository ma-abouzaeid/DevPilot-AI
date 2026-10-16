using System.Text.Json.Serialization;

namespace DevPilotAI.Application.Common.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<ValidationErrorDetail> Errors { get; set; } = new();

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }

    [JsonPropertyName("requestId")]
    public string? RequestId => TraceId;

    public static ApiResponse<T> SuccessResult(T data, string message = "Operation completed successfully.", string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = new List<ValidationErrorDetail>(),
            ErrorCode = null,
            TraceId = traceId
        };
    }

    public static ApiResponse<T> FailureResult(string message, string errorCode, List<ValidationErrorDetail>? errors = null, string? traceId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = errors ?? new List<ValidationErrorDetail>(),
            ErrorCode = errorCode,
            TraceId = traceId
        };
    }
}

public class ValidationErrorDetail
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = "VALIDATION_ERROR";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    public ValidationErrorDetail() { }

    public ValidationErrorDetail(string field, string message, string code = "VALIDATION_ERROR")
    {
        Field = field;
        Message = message;
        Code = code;
    }
}
