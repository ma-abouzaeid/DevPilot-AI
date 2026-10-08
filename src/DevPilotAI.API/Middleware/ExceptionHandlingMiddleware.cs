using System.Net;
using System.Text.Json;
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
            _logger.LogError(ex, "Unhandled exception encountered during HTTP request processing.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, errorCode, message) = exception switch
        {
            KeyNotFoundException keyEx => (HttpStatusCode.NotFound, "NOT_FOUND", keyEx.Message),
            ArgumentException argEx => (HttpStatusCode.BadRequest, "BAD_REQUEST", argEx.Message),
            UnauthorizedAccessException authEx => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", authEx.Message),
            InvalidOperationException invEx => (HttpStatusCode.Conflict, "CONFLICT", invEx.Message),
            DbUpdateException => (HttpStatusCode.Conflict, "CONFLICT", "A resource conflict occurred. The record may already exist."),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR", "An unexpected error occurred. Please try again later.")
        };

        context.Response.StatusCode = (int)statusCode;

        var requestId = context.TraceIdentifier;

        var responsePayload = new
        {
            success = false,
            errorCode = errorCode,
            message = message,
            requestId = requestId
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(responsePayload));
    }
}
