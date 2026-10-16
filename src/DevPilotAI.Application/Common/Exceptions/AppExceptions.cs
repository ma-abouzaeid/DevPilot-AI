using System.Net;
using DevPilotAI.Application.Common.Models;

namespace DevPilotAI.Application.Common.Exceptions;

public interface IAppException
{
    HttpStatusCode StatusCode { get; }
    string ErrorCode { get; }
    string Message { get; }
    List<ValidationErrorDetail> Errors { get; }
}

public static class AppError
{
    public static InvalidOperationException Conflict(string message, string errorCode = "RESOURCE_CONFLICT")
    {
        var ex = new InvalidOperationException(message);
        ex.Data["ErrorCode"] = errorCode;
        return ex;
    }

    public static UnauthorizedAccessException Unauthorized(string message, string errorCode = "UNAUTHORIZED")
    {
        var ex = new UnauthorizedAccessException(message);
        ex.Data["ErrorCode"] = errorCode;
        return ex;
    }

    public static UnauthorizedAccessException Forbidden(string message = "You do not have permission to access or modify this resource.", string errorCode = "FORBIDDEN")
    {
        var ex = new UnauthorizedAccessException(message);
        ex.Data["ErrorCode"] = errorCode;
        ex.Data["StatusCode"] = HttpStatusCode.Forbidden;
        return ex;
    }

    public static KeyNotFoundException NotFound(string message, string errorCode = "RESOURCE_NOT_FOUND")
    {
        var ex = new KeyNotFoundException(message);
        ex.Data["ErrorCode"] = errorCode;
        return ex;
    }

    public static ArgumentException InvalidRequest(string message, string errorCode = "INVALID_REQUEST", string? paramName = null)
    {
        var ex = paramName != null ? new ArgumentException(message, paramName) : new ArgumentException(message);
        ex.Data["ErrorCode"] = errorCode;
        return ex;
    }
}
