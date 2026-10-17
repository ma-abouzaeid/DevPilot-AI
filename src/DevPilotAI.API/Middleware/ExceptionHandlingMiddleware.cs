using System.Net;
using System.Text.Json;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(ex, "The response has already started, cannot write exception response.");
                throw;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var traceId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.ToString().Trim()
            : context.TraceIdentifier;

        var (statusCode, errorCode, message, errors) = exception switch
        {
            IAppException appEx => ((int)appEx.StatusCode, appEx.ErrorCode, appEx.Message, appEx.Errors),
            KeyNotFoundException keyEx => ((int)HttpStatusCode.NotFound, (string?)keyEx.Data["ErrorCode"] ?? "RESOURCE_NOT_FOUND", keyEx.Message, new List<ValidationErrorDetail>()),
            ArgumentException argEx => ((int)HttpStatusCode.BadRequest, (string?)argEx.Data["ErrorCode"] ?? "INVALID_REQUEST", argEx.Message, new List<ValidationErrorDetail>()),
            UnauthorizedAccessException authEx => HandleUnauthorizedAccess(context, authEx),
            InvalidOperationException invEx => (invEx.Data["StatusCode"] is HttpStatusCode sc ? (int)sc : (int)HttpStatusCode.Conflict, (string?)invEx.Data["ErrorCode"] ?? "RESOURCE_CONFLICT", invEx.Message, new List<ValidationErrorDetail>()),
            DbUpdateException dbEx => HandleDbUpdateException(dbEx),
            _ => ((int)HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected error occurred. Please try again later.", new List<ValidationErrorDetail>())
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Server error processing {Method} {Path}. ErrorCode: {ErrorCode}, StatusCode: {StatusCode}, TraceId: {TraceId}",
                context.Request.Method, context.Request.Path, errorCode, statusCode, traceId);
        }
        else
        {
            _logger.LogWarning("Handled application failure processing {Method} {Path}: {Message}. ErrorCode: {ErrorCode}, StatusCode: {StatusCode}, TraceId: {TraceId}",
                context.Request.Method, context.Request.Path, message, errorCode, statusCode, traceId);
        }

        context.Response.StatusCode = statusCode;

        var responsePayload = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Data = null,
            Errors = errors,
            ErrorCode = errorCode,
            TraceId = traceId
        };

        var json = JsonSerializer.Serialize(responsePayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        });

        await context.Response.WriteAsync(json);
    }

    private static (int StatusCode, string ErrorCode, string Message, List<ValidationErrorDetail> Errors) HandleUnauthorizedAccess(HttpContext context, UnauthorizedAccessException authEx)
    {
        int statusCode;
        string defaultErrorCode;

        if (authEx.Data["StatusCode"] is HttpStatusCode sc)
        {
            statusCode = (int)sc;
            defaultErrorCode = statusCode == (int)HttpStatusCode.Forbidden ? "FORBIDDEN" : "UNAUTHORIZED";
        }
        else if (context.User?.Identity?.IsAuthenticated == true)
        {
            statusCode = (int)HttpStatusCode.Forbidden;
            defaultErrorCode = "FORBIDDEN";
        }
        else
        {
            statusCode = (int)HttpStatusCode.Unauthorized;
            defaultErrorCode = "UNAUTHORIZED";
        }

        var errorCode = (string?)authEx.Data["ErrorCode"] ?? defaultErrorCode;
        return (statusCode, errorCode, authEx.Message, new List<ValidationErrorDetail>());
    }

    private static (int StatusCode, string ErrorCode, string Message, List<ValidationErrorDetail> Errors) HandleDbUpdateException(DbUpdateException dbEx)
    {
        if (dbEx is DbUpdateConcurrencyException)
        {
            return ((int)HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT", "The resource was modified by another operation. Please reload and try again.", new List<ValidationErrorDetail>());
        }

        if (dbEx.InnerException is SqlException sqlEx)
        {
            if (sqlEx.Number == 2601 || sqlEx.Number == 2627)
            {
                return ((int)HttpStatusCode.Conflict, "RESOURCE_CONFLICT", "A resource with these details already exists.", new List<ValidationErrorDetail>());
            }

            if (sqlEx.Number == 547)
            {
                return ((int)HttpStatusCode.BadRequest, "FOREIGN_KEY_VIOLATION", "A referenced record does not exist or cannot be modified.", new List<ValidationErrorDetail>());
            }
        }

        var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
        if (innerMsg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
            innerMsg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
            innerMsg.Contains("IX_", StringComparison.OrdinalIgnoreCase))
        {
            return ((int)HttpStatusCode.Conflict, "RESOURCE_CONFLICT", "A resource with these details already exists.", new List<ValidationErrorDetail>());
        }

        return ((int)HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected database error occurred while persisting changes.", new List<ValidationErrorDetail>());
    }
}
