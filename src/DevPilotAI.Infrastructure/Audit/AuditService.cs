using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DevPilotAI.Infrastructure.Audit;

public class AuditService : IAuditService
{
    private static readonly string[] SensitiveKeywords = new[]
    {
        "password",
        "token",
        "secret",
        "key",
        "authorization",
        "cookie",
        "credential",
        "hash"
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEvent
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            UserId = userId
        };

        await LogAsync(auditEvent, cancellationToken);
    }

    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var ipAddress = auditEvent.IpAddress;
            var userAgent = auditEvent.UserAgent;
            var requestId = auditEvent.RequestId;
            var userId = auditEvent.UserId;

            if (httpContext != null)
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                }

                if (string.IsNullOrWhiteSpace(userAgent))
                {
                    userAgent = httpContext.Request.Headers.UserAgent.ToString();
                }

                if (string.IsNullOrWhiteSpace(requestId))
                {
                    if (httpContext.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId) && !string.IsNullOrWhiteSpace(correlationId))
                    {
                        requestId = correlationId.ToString().Trim();
                    }
                    else
                    {
                        requestId = httpContext.TraceIdentifier;
                    }
                }

                if (!userId.HasValue)
                {
                    var userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdClaim, out var parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                }
            }

            var serializedOldValues = SanitizeAndSerialize(auditEvent.OldValues);
            var serializedNewValues = SanitizeAndSerialize(auditEvent.NewValues);

            var log = new AuditLog
            {
                Action = Truncate(auditEvent.Action, 100) ?? string.Empty,
                EntityName = Truncate(auditEvent.EntityName, 100) ?? string.Empty,
                EntityId = Truncate(auditEvent.EntityId, 100) ?? string.Empty,
                UserId = userId,
                OldValues = serializedOldValues,
                NewValues = serializedNewValues,
                IpAddress = Truncate(ipAddress, 100),
                UserAgent = Truncate(userAgent, 500),
                RequestId = Truncate(requestId, 100),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AuditLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log for action {Action} on entity {EntityName}", auditEvent.Action, auditEvent.EntityName);
        }
    }

    private static string? SanitizeAndSerialize(object? value)
    {
        if (value == null)
        {
            return null;
        }

        try
        {
            JsonNode? node;
            if (value is string stringVal)
            {
                if (string.IsNullOrWhiteSpace(stringVal))
                {
                    return null;
                }

                try
                {
                    node = JsonNode.Parse(stringVal);
                }
                catch
                {
                    node = JsonValue.Create(stringVal);
                }
            }
            else
            {
                var json = JsonSerializer.Serialize(value);
                node = JsonNode.Parse(json);
            }

            if (node == null)
            {
                return null;
            }

            SanitizeJsonNode(node);
            return node.ToJsonString();
        }
        catch
        {
            return "[SERIALIZATION_FAILED]";
        }
    }

    private static void SanitizeJsonNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var properties = obj.ToList();
            foreach (var property in properties)
            {
                if (IsSensitiveKey(property.Key))
                {
                    obj[property.Key] = "[REDACTED]";
                }
                else if (property.Value != null)
                {
                    SanitizeJsonNode(property.Value);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null)
                {
                    SanitizeJsonNode(item);
                }
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        for (var i = 0; i < SensitiveKeywords.Length; i++)
        {
            if (key.Contains(SensitiveKeywords[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }
}
