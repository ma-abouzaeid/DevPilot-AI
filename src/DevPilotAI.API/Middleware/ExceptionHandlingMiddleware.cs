using System.Net;
using System.Security.Claims;
using System.Text.Json;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
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

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
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

            await HandleExceptionAsync(context, ex, scopeFactory);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, IServiceScopeFactory scopeFactory)
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
            SqlException sqlEx => HandleSqlException(sqlEx),
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

        await PersistSystemErrorAsync(context, exception, scopeFactory, statusCode, errorCode, message, traceId, json);

        await context.Response.WriteAsync(json);
    }

    private async Task PersistSystemErrorAsync(
        HttpContext context,
        Exception exception,
        IServiceScopeFactory scopeFactory,
        int statusCode,
        string errorCode,
        string message,
        string traceId,
        string responseJson)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DevPilotDbContext>();

            int? userId = null;
            var claim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claim, out var parsed) && parsed > 0)
            {
                userId = parsed;
            }

            var endpoint = context.GetEndpoint()?.DisplayName;
            var action = !string.IsNullOrWhiteSpace(endpoint)
                ? ClipRequired(endpoint, 150)
                : ClipRequired($"{context.Request.Method} {context.Request.Path}", 150);

            db.SystemErrorLogs.Add(new SystemErrorLog
            {
                TraceId = ClipRequired(traceId, 100),
                HttpMethod = ClipRequired(context.Request.Method, 20),
                Path = ClipRequired(context.Request.Path.Value ?? "/", 500),
                QueryString = ClipOptional(context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null, 1000),
                Action = action ?? "HTTP_REQUEST",
                StatusCode = statusCode,
                ErrorCode = ClipRequired(errorCode, 100),
                Message = ClipRequired(message, 2000),
                ExceptionType = ClipOptional(exception.GetType().FullName, 300),
                ExceptionMessage = ClipOptional(exception.ToString(), 4000),
                StackTrace = ClipOptional(exception.StackTrace, 8000),
                ResponseJson = ClipOptional(responseJson, 8000),
                UserId = userId,
                IpAddress = ClipOptional(context.Connection.RemoteIpAddress?.ToString(), 100),
                UserAgent = ClipOptional(context.Request.Headers.UserAgent.ToString(), 500),
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (Exception persistEx)
        {
            _logger.LogWarning(persistEx, "Failed to persist SystemErrorLog for {Path}", context.Request.Path);
        }
    }

    private static string ClipRequired(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return value.Length <= max ? value : value[..max];
    }

    private static string? ClipOptional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Length <= max ? value : value[..max];
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
            return HandleSqlException(sqlEx);
        }

        var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
        if (innerMsg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
            innerMsg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
            innerMsg.Contains("IX_", StringComparison.OrdinalIgnoreCase))
        {
            return ((int)HttpStatusCode.Conflict, "RESOURCE_CONFLICT", "A resource with these details already exists.", new List<ValidationErrorDetail>());
        }

        if (innerMsg.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase) ||
            innerMsg.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            return ((int)HttpStatusCode.InternalServerError, "DATABASE_SCHEMA_MISMATCH",
                "The database schema is out of date. Apply pending SQL scripts/migrations and try again.",
                new List<ValidationErrorDetail>());
        }

        return ((int)HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected database error occurred while persisting changes.", new List<ValidationErrorDetail>());
    }

    private static (int StatusCode, string ErrorCode, string Message, List<ValidationErrorDetail> Errors) HandleSqlException(SqlException sqlEx)
    {
        // 207 = invalid column, 208 = invalid object
        if (sqlEx.Number == 207 || sqlEx.Number == 208)
        {
            return ((int)HttpStatusCode.InternalServerError, "DATABASE_SCHEMA_MISMATCH",
                "The database schema is out of date. Apply pending SQL scripts/migrations and try again.",
                new List<ValidationErrorDetail>());
        }

        if (sqlEx.Number == 2601 || sqlEx.Number == 2627)
        {
            return ((int)HttpStatusCode.Conflict, "RESOURCE_CONFLICT", "A resource with these details already exists.", new List<ValidationErrorDetail>());
        }

        if (sqlEx.Number == 547)
        {
            return ((int)HttpStatusCode.BadRequest, "FOREIGN_KEY_VIOLATION", "A referenced record does not exist or cannot be modified.", new List<ValidationErrorDetail>());
        }

        return ((int)HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected database error occurred while persisting changes.", new List<ValidationErrorDetail>());
    }
}
