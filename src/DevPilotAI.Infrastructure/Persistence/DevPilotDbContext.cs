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
