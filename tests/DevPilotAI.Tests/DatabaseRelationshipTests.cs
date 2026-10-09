using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DevPilotAI.Tests;

public class DatabaseRelationshipTests
{
    private DevPilotDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<DevPilotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new DevPilotDbContext(options);
    }

    [Fact]
    public void ModelConfiguration_EveryForeignKey_MustNotUseCascadeDelete()
    {
        using var context = GetDbContext();
        var model = context.Model;

        var foreignKeys = model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).ToList();

        Assert.NotEmpty(foreignKeys);

        foreach (var fk in foreignKeys)
        {
            Assert.NotEqual(DeleteBehavior.Cascade, fk.DeleteBehavior);
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        }
    }

    [Fact]
    public async Task DeletingParentUser_WithChildRefreshToken_IsBlockedFromCascadeDeletingChild()
    {
        using var context = GetDbContext();

        var user = new User
        {
            Name = "Cascade Test User",
            Email = "cascade-test@example.com",
            PasswordHash = "HashedPassword123"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var refreshToken = new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = "test_hash_12345",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        context.RefreshTokens.Add(refreshToken);
        await context.SaveChangesAsync();

        Assert.Throws<InvalidOperationException>(() => context.Users.Remove(user));

        var remainingToken = await context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == "test_hash_12345");

        Assert.NotNull(remainingToken);
        Assert.Equal(user.UserId, remainingToken.UserId);
    }

    [Fact]
    public async Task DeletingParentUser_WithChildAuditLog_DoesNotCascadeDeleteAuditLog()
    {
        using var context = GetDbContext();

        var user = new User
        {
            Name = "Audit Cascade Test User",
            Email = "audit-cascade-test@example.com",
            PasswordHash = "HashedPassword123"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditLog = new AuditLog
        {
            UserId = user.UserId,
            Action = "TEST_CASCADE",
            EntityName = "User",
            EntityId = user.UserId.ToString()
        };
        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        context.Users.Remove(user);
        await context.SaveChangesAsync();

        var remainingLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.AuditLogId == auditLog.AuditLogId);

        Assert.NotNull(remainingLog);
    }

    [Fact]
    public async Task DeletingParentProblem_WithChildProblemRequest_IsBlockedFromCascadeDeletingChild()
    {
        using var context = GetDbContext();

        var user = new User
        {
            Name = "Problem User",
            Email = "problem-user@example.com",
            PasswordHash = "HashedPassword123"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var problem = new Problem
        {
            ProblemCode = "PROB-TEST-001",
            UserId = user.UserId,
            Title = "Test Problem",
            Summary = "Test Summary"
        };
        context.Problems.Add(problem);
        await context.SaveChangesAsync();

        var request = new ProblemRequest
        {
            ProblemId = problem.ProblemId,
            Description = "Request description"
        };
        context.ProblemRequests.Add(request);
        await context.SaveChangesAsync();

        Assert.Throws<InvalidOperationException>(() => context.Problems.Remove(problem));

        var remainingRequest = await context.ProblemRequests
            .FirstOrDefaultAsync(pr => pr.ProblemRequestId == request.ProblemRequestId);

        Assert.NotNull(remainingRequest);
        Assert.Equal(problem.ProblemId, remainingRequest.ProblemId);
    }
}
