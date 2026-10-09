using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Services;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using DevPilotAI.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class PlanAndUsageEntityTests
{
    private DevPilotDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<DevPilotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new DevPilotDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void PlanEntity_Configuration_HasPrimaryKeyAndUniqueIndex()
    {
        using var context = GetDbContext();
        var entityType = context.Model.FindEntityType(typeof(Plan));

        Assert.NotNull(entityType);
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal(nameof(Plan.PlanId), pk.Properties[0].Name);

        var indexes = entityType.GetIndexes().ToList();
        var codeIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Plan.Code)));
        Assert.NotNull(codeIndex);
        Assert.True(codeIndex.IsUnique);
    }

    [Fact]
    public void FeatureEntity_Configuration_HasPrimaryKeyAndUniqueIndex()
    {
        using var context = GetDbContext();
        var entityType = context.Model.FindEntityType(typeof(Feature));

        Assert.NotNull(entityType);
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal(nameof(Feature.FeatureId), pk.Properties[0].Name);

        var indexes = entityType.GetIndexes().ToList();
        var keyIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Feature.Key)));
        Assert.NotNull(keyIndex);
        Assert.True(keyIndex.IsUnique);
    }

    [Fact]
    public void PlanFeature_Configuration_HasCompositePrimaryKeyAndNoActionDelete()
    {
        using var context = GetDbContext();
        var entityType = context.Model.FindEntityType(typeof(PlanFeature));

        Assert.NotNull(entityType);
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Equal(2, pk.Properties.Count);
        Assert.Contains(pk.Properties, p => p.Name == nameof(PlanFeature.PlanId));
        Assert.Contains(pk.Properties, p => p.Name == nameof(PlanFeature.FeatureId));

        var foreignKeys = entityType.GetForeignKeys().ToList();
        Assert.Equal(2, foreignKeys.Count);
        foreach (var fk in foreignKeys)
        {
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        }
    }

    [Fact]
    public void UserSubscription_Configuration_HasBigIntKeyAndNoCascadeDelete()
    {
        using var context = GetDbContext();
        var entityType = context.Model.FindEntityType(typeof(UserSubscription));

        Assert.NotNull(entityType);
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal(nameof(UserSubscription.SubscriptionId), pk.Properties[0].Name);
        Assert.Equal(typeof(long), pk.Properties[0].ClrType);

        var foreignKeys = entityType.GetForeignKeys().ToList();
        Assert.NotEmpty(foreignKeys);
        foreach (var fk in foreignKeys)
        {
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        }
    }

    [Fact]
    public void AIUsageEvent_Configuration_HasBigIntKeyCostPrecisionAndNoCascadeDelete()
    {
        using var context = GetDbContext();
        var entityType = context.Model.FindEntityType(typeof(AIUsageEvent));

        Assert.NotNull(entityType);
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal(nameof(AIUsageEvent.AIUsageEventId), pk.Properties[0].Name);
        Assert.Equal(typeof(long), pk.Properties[0].ClrType);

        var estCostProp = entityType.FindProperty(nameof(AIUsageEvent.EstimatedCost));
        Assert.NotNull(estCostProp);
        Assert.Equal(18, estCostProp.GetPrecision());
        Assert.Equal(6, estCostProp.GetScale());

        var actCostProp = entityType.FindProperty(nameof(AIUsageEvent.ActualCost));
        Assert.NotNull(actCostProp);
        Assert.Equal(18, actCostProp.GetPrecision());
        Assert.Equal(6, actCostProp.GetScale());

        var foreignKeys = entityType.GetForeignKeys().ToList();
        Assert.NotEmpty(foreignKeys);
        foreach (var fk in foreignKeys)
        {
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        }
    }

    [Fact]
    public void UsagePeriod_Configuration_HasBigIntKeyUniqueIndexAndNoCascadeDelete()
    {
        using var context = GetDbContext();
        var entityType = context.Model.FindEntityType(typeof(UsagePeriod));

        Assert.NotNull(entityType);
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal(nameof(UsagePeriod.UsagePeriodId), pk.Properties[0].Name);
        Assert.Equal(typeof(long), pk.Properties[0].ClrType);

        var uniqueIndex = entityType.GetIndexes().FirstOrDefault(i => i.IsUnique);
        Assert.NotNull(uniqueIndex);
        var propNames = uniqueIndex.Properties.Select(p => p.Name).ToList();
        Assert.Contains(nameof(UsagePeriod.UserId), propNames);
        Assert.Contains(nameof(UsagePeriod.PeriodStart), propNames);
        Assert.Contains(nameof(UsagePeriod.PeriodEnd), propNames);

        var foreignKeys = entityType.GetForeignKeys().ToList();
        Assert.NotEmpty(foreignKeys);
        foreach (var fk in foreignKeys)
        {
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        }
    }

    [Fact]
    public void UsageLedger_Configuration_HasBigIntKeyAndNoCascadeDelete()
    {
        using var context = GetDbContext();
        var entityType = context.Model.FindEntityType(typeof(UsageLedger));

        Assert.NotNull(entityType);
        var pk = entityType.FindPrimaryKey();
        Assert.NotNull(pk);
        Assert.Single(pk.Properties);
        Assert.Equal(nameof(UsageLedger.UsageLedgerId), pk.Properties[0].Name);
        Assert.Equal(typeof(long), pk.Properties[0].ClrType);

        var foreignKeys = entityType.GetForeignKeys().ToList();
        Assert.NotEmpty(foreignKeys);
        foreach (var fk in foreignKeys)
        {
            Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
        }
    }

    [Fact]
    public async Task SeedData_ContainsDefaultFreePlanAndCoreFeatures()
    {
        using var context = GetDbContext();

        var freePlan = await context.Plans.FirstOrDefaultAsync(p => p.Code == "FREE");
        Assert.NotNull(freePlan);
        Assert.True(freePlan.IsDefault);
        Assert.Equal("Active", freePlan.Status);
        Assert.Equal("Monthly", freePlan.PeriodType);
        Assert.Equal(100, freePlan.CreditsPerPeriod);

        var features = await context.Features.ToListAsync();
        Assert.Contains(features, f => f.Key == "ai_debugging");
        Assert.Contains(features, f => f.Key == "monthly_ai_credits");
    }

    [Fact]
    public async Task RegisterAsync_WhenDefaultPlanExists_AssignsFreePlanUserSubscription()
    {
        using var context = GetDbContext();

        var mockEmailService = new Mock<IEmailService>();
        mockEmailService.Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockTurnstileService = new Mock<ITurnstileService>();
        mockTurnstileService.Setup(t => t.VerifyTokenAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JwtSettings:Secret", "DevPilotAI_Super_Secret_Key_Change_In_Production_123456789!" },
            { "JwtSettings:Issuer", "DevPilotAI" },
            { "JwtSettings:Audience", "DevPilotAIUsers" },
            { "JwtSettings:ExpiryMinutes", "60" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var tokenGenerator = new JwtTokenGenerator(configuration);

        var authService = new AuthService(
            context,
            mockTurnstileService.Object,
            tokenGenerator,
            mockEmailService.Object,
            auditService: null
        );

        var registerDto = new RegisterRequestDto
        {
            Name = "New Subscribed User",
            Email = "subscribed@example.com",
            Password = "SecurePassword123!",
            TurnstileToken = "valid-token"
        };

        var result = await authService.RegisterAsync(registerDto);

        Assert.True(result.Success);

        var createdUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "subscribed@example.com");
        Assert.NotNull(createdUser);

        var subscription = await context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == createdUser.UserId);
        Assert.NotNull(subscription);
        Assert.Equal(1, subscription.PlanId);
        Assert.Equal("Active", subscription.Status);
        Assert.Equal("Registration", subscription.AssignmentSource);
    }
}
