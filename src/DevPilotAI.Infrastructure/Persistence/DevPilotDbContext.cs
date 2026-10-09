using System.Linq.Expressions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Infrastructure.Persistence;

public class DevPilotDbContext : DbContext, IApplicationDbContext
{
    public DevPilotDbContext(DbContextOptions<DevPilotDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<ProblemRequest> ProblemRequests => Set<ProblemRequest>();
    public DbSet<ProblemAttachment> ProblemAttachments => Set<ProblemAttachment>();
    public DbSet<DebuggingAttempt> DebuggingAttempts => Set<DebuggingAttempt>();
    public DbSet<DeveloperFeedback> DeveloperFeedbacks => Set<DeveloperFeedback>();
    public DbSet<ProblemSolution> ProblemSolutions => Set<ProblemSolution>();

    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<ArticleVersion> ArticleVersions => Set<ArticleVersion>();

    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<ChallengeAttempt> ChallengeAttempts => Set<ChallengeAttempt>();
    public DbSet<ChallengeEvaluation> ChallengeEvaluations => Set<ChallengeEvaluation>();

    public DbSet<GitHubAccount> GitHubAccounts => Set<GitHubAccount>();
    public DbSet<GitHubRepository> GitHubRepositories => Set<GitHubRepository>();
    public DbSet<CodeReview> CodeReviews => Set<CodeReview>();
    public DbSet<CodeReviewFinding> CodeReviewFindings => Set<CodeReviewFinding>();

    public DbSet<AIProvider> AIProviders => Set<AIProvider>();
    public DbSet<AIModel> AIModels => Set<AIModel>();
    public DbSet<AIRequest> AIRequests => Set<AIRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<AIUsageEvent> AIUsageEvents => Set<AIUsageEvent>();
    public DbSet<UsagePeriod> UsagePeriods => Set<UsagePeriod>();
    public DbSet<UsageLedger> UsageLedgers => Set<UsageLedger>();

    public void SoftDelete<TEntity>(TEntity entity) where TEntity : class, ISoftDeletable
    {
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        Update(entity);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        modelBuilder.Entity<EmailVerification>()
            .HasKey(ev => ev.VerificationId);

        modelBuilder.Entity<RefreshToken>()
            .HasKey(rt => rt.RefreshTokenId);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.TokenHash);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Problem>()
            .HasIndex(p => p.ProblemCode)
            .IsUnique();

        modelBuilder.Entity<AIProvider>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(a => a.AuditLogId);
            b.Property(a => a.Action).HasMaxLength(100).IsRequired();
            b.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
            b.Property(a => a.EntityId).HasMaxLength(100).IsRequired();
            b.Property(a => a.IpAddress).HasMaxLength(100);
            b.Property(a => a.UserAgent).HasMaxLength(500);
            b.Property(a => a.RequestId).HasMaxLength(100);

            b.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(a => a.CreatedAt);
            b.HasIndex(a => a.UserId);
            b.HasIndex(a => a.Action);
            b.HasIndex(a => a.RequestId);
        });

        modelBuilder.Entity<Plan>(b =>
        {
            b.HasKey(p => p.PlanId);
            b.HasIndex(p => p.Code).IsUnique();
            b.Property(p => p.Code).HasMaxLength(50).IsRequired();
            b.Property(p => p.Name).HasMaxLength(100).IsRequired();
            b.Property(p => p.Description).HasMaxLength(500);
            b.Property(p => p.Status).HasMaxLength(50).IsRequired();
            b.Property(p => p.PeriodType).HasMaxLength(50).IsRequired();
            b.Property(p => p.Price).HasPrecision(18, 2);
            b.Property(p => p.Currency).HasMaxLength(10);
        });

        modelBuilder.Entity<Feature>(b =>
        {
            b.HasKey(f => f.FeatureId);
            b.HasIndex(f => f.Key).IsUnique();
            b.Property(f => f.Key).HasMaxLength(100).IsRequired();
            b.Property(f => f.Name).HasMaxLength(150).IsRequired();
            b.Property(f => f.Description).HasMaxLength(500);
            b.Property(f => f.Category).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<PlanFeature>(b =>
        {
            b.HasKey(pf => new { pf.PlanId, pf.FeatureId });
            b.Property(pf => pf.ConfigJson).HasColumnType("nvarchar(max)");

            b.HasOne(pf => pf.Plan)
                .WithMany(p => p.PlanFeatures)
                .HasForeignKey(pf => pf.PlanId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(pf => pf.Feature)
                .WithMany(f => f.PlanFeatures)
                .HasForeignKey(pf => pf.FeatureId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserSubscription>(b =>
        {
            b.HasKey(us => us.SubscriptionId);
            b.Property(us => us.Status).HasMaxLength(50).IsRequired();
            b.Property(us => us.AssignmentSource).HasMaxLength(50).IsRequired();
            b.Property(us => us.CancellationReason).HasMaxLength(255);

            b.HasOne(us => us.User)
                .WithMany(u => u.UserSubscriptions)
                .HasForeignKey(us => us.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(us => us.Plan)
                .WithMany(p => p.UserSubscriptions)
                .HasForeignKey(us => us.PlanId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(us => us.AssignedByUser)
                .WithMany()
                .HasForeignKey(us => us.AssignedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(us => us.UserId);
            b.HasIndex(us => us.PlanId);
            b.HasIndex(us => us.Status);
        });

        modelBuilder.Entity<AIUsageEvent>(b =>
        {
            b.HasKey(aue => aue.AIUsageEventId);
            b.Property(aue => aue.OperationType).HasMaxLength(100).IsRequired();
            b.Property(aue => aue.CostType).HasMaxLength(50).IsRequired();
            b.Property(aue => aue.Status).HasMaxLength(50).IsRequired();
            b.Property(aue => aue.ProviderRequestId).HasMaxLength(150);
            b.Property(aue => aue.CorrelationId).HasMaxLength(150);
            b.Property(aue => aue.IdempotencyKey).HasMaxLength(150);
            b.Property(aue => aue.ErrorCode).HasMaxLength(100);
            b.Property(aue => aue.EstimatedCost).HasPrecision(18, 6);
            b.Property(aue => aue.ActualCost).HasPrecision(18, 6);

            b.HasOne(aue => aue.AIRequest)
                .WithMany(r => r.AIUsageEvents)
                .HasForeignKey(aue => aue.AIRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(aue => aue.User)
                .WithMany(u => u.AIUsageEvents)
                .HasForeignKey(aue => aue.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(aue => aue.Problem)
                .WithMany(p => p.AIUsageEvents)
                .HasForeignKey(aue => aue.ProblemId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(aue => aue.Provider)
                .WithMany(p => p.AIUsageEvents)
                .HasForeignKey(aue => aue.ProviderId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(aue => aue.Model)
                .WithMany(m => m.AIUsageEvents)
                .HasForeignKey(aue => aue.ModelId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(aue => aue.UserId);
            b.HasIndex(aue => aue.AIRequestId);
            b.HasIndex(aue => aue.CreatedAt);
            b.HasIndex(aue => aue.OperationType);
        });

        modelBuilder.Entity<UsagePeriod>(b =>
        {
            b.HasKey(up => up.UsagePeriodId);

            b.HasOne(up => up.User)
                .WithMany(u => u.UsagePeriods)
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(up => up.Plan)
                .WithMany(p => p.UsagePeriods)
                .HasForeignKey(up => up.PlanId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(up => new { up.UserId, up.PeriodStart, up.PeriodEnd }).IsUnique();
            b.HasIndex(up => up.UserId);
        });

        modelBuilder.Entity<UsageLedger>(b =>
        {
            b.HasKey(ul => ul.UsageLedgerId);
            b.Property(ul => ul.EntryType).HasMaxLength(50).IsRequired();
            b.Property(ul => ul.Reason).HasMaxLength(255).IsRequired();
            b.Property(ul => ul.ReferenceId).HasMaxLength(150);
            b.Property(ul => ul.MetadataJson).HasColumnType("nvarchar(max)");

            b.HasOne(ul => ul.User)
                .WithMany(u => u.UsageLedgers)
                .HasForeignKey(ul => ul.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(ul => ul.UsagePeriod)
                .WithMany(p => p.UsageLedgers)
                .HasForeignKey(ul => ul.UsagePeriodId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(ul => ul.AIUsageEvent)
                .WithMany(e => e.UsageLedgers)
                .HasForeignKey(ul => ul.AIUsageEventId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(ul => ul.UserId);
            b.HasIndex(ul => ul.UsagePeriodId);
            b.HasIndex(ul => ul.AIUsageEventId);
            b.HasIndex(ul => ul.CreatedAt);
        });

        modelBuilder.Entity<Plan>().HasData(
            new Plan
            {
                PlanId = 1,
                Name = "Free",
                Code = "FREE",
                Description = "Default Free tier with standard AI quota.",
                Status = "Active",
                IsDefault = true,
                IsPublic = true,
                PeriodType = "Monthly",
                CreditsPerPeriod = 100,
                Price = 0.00m,
                Currency = "USD",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<Feature>().HasData(
            new Feature
            {
                FeatureId = 1,
                Key = "ai_debugging",
                Name = "AI Debugging",
                Description = "Access to AI debugging assistant.",
                Category = "AI",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Feature
            {
                FeatureId = 2,
                Key = "monthly_ai_credits",
                Name = "Monthly AI Credits",
                Description = "Total AI credits available per billing cycle.",
                Category = "Quota",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<PlanFeature>().HasData(
            new PlanFeature
            {
                PlanId = 1,
                FeatureId = 1,
                IsEnabled = true,
                LimitValue = 100,
                ConfigJson = "{\"enabled\": true}",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new PlanFeature
            {
                PlanId = 1,
                FeatureId = 2,
                IsEnabled = true,
                LimitValue = 100,
                ConfigJson = "{\"credits\": 100}",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var falseConstant = Expression.Constant(false);
                var equalExpression = Expression.Equal(property, falseConstant);
                var lambda = Expression.Lambda(equalExpression, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }
}
