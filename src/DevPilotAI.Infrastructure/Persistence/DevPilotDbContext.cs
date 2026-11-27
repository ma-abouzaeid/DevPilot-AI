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
    public DbSet<ProblemEmbedding> ProblemEmbeddings => Set<ProblemEmbedding>();
    public DbSet<SimilarityMatch> SimilarityMatches => Set<SimilarityMatch>();

    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<ArticleVersion> ArticleVersions => Set<ArticleVersion>();
    public DbSet<ArticleLike> ArticleLikes => Set<ArticleLike>();
    public DbSet<ArticleSave> ArticleSaves => Set<ArticleSave>();
    public DbSet<ArticleComment> ArticleComments => Set<ArticleComment>();

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
    public DbSet<SystemErrorLog> SystemErrorLogs => Set<SystemErrorLog>();

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<AIUsageEvent> AIUsageEvents => Set<AIUsageEvent>();
    public DbSet<UsagePeriod> UsagePeriods => Set<UsagePeriod>();
    public DbSet<UsageLedger> UsageLedgers => Set<UsageLedger>();
    public DbSet<CreditActionCost> CreditActionCosts => Set<CreditActionCost>();

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<TelegramConversation> TelegramConversations => Set<TelegramConversation>();
    public DbSet<UserFeedback> UserFeedbacks => Set<UserFeedback>();

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

        modelBuilder.Entity<EmailVerification>(b =>
        {
            b.HasKey(ev => ev.VerificationId);
            b.Property(ev => ev.PendingEmail).HasMaxLength(255);
            b.Property(ev => ev.Method).HasMaxLength(50);
        });

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

        modelBuilder.Entity<ProblemEmbedding>(b =>
        {
            b.HasKey(pe => pe.EmbeddingId);
            b.Property(pe => pe.EmbeddingData).HasColumnType("varbinary(max)").IsRequired();
            b.Property(pe => pe.ModelVersion).HasMaxLength(100).IsRequired();

            b.HasOne(pe => pe.ProblemRequest)
                .WithOne(pr => pr.Embedding)
                .HasForeignKey<ProblemEmbedding>(pe => pe.ProblemRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(pe => pe.ProblemRequestId).IsUnique();
        });

        modelBuilder.Entity<SimilarityMatch>(b =>
        {
            b.HasKey(sm => sm.SimilarityMatchId);
            b.Property(sm => sm.SimilarityScore).HasPrecision(6, 5);

            b.HasOne(sm => sm.ProblemRequest)
                .WithMany(pr => pr.SimilarityMatches)
                .HasForeignKey(sm => sm.ProblemRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(sm => sm.MatchedProblem)
                .WithMany(p => p.SimilarityMatches)
                .HasForeignKey(sm => sm.MatchedProblemId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(sm => sm.ProblemRequestId);
            b.HasIndex(sm => sm.MatchedProblemId);
        });

        modelBuilder.Entity<ArticleLike>(b =>
        {
            b.HasKey(x => x.ArticleLikeId);
            b.HasIndex(x => new { x.KnowledgeArticleId, x.UserId }).IsUnique();
            b.HasOne(x => x.KnowledgeArticle)
                .WithMany(a => a.Likes)
                .HasForeignKey(x => x.KnowledgeArticleId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.User)
                .WithMany(u => u.ArticleLikes)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ArticleSave>(b =>
        {
            b.HasKey(x => x.ArticleSaveId);
            b.HasIndex(x => new { x.KnowledgeArticleId, x.UserId }).IsUnique();
            b.HasOne(x => x.KnowledgeArticle)
                .WithMany(a => a.Saves)
                .HasForeignKey(x => x.KnowledgeArticleId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.User)
                .WithMany(u => u.ArticleSaves)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ArticleComment>(b =>
        {
            b.HasKey(x => x.ArticleCommentId);
            b.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            b.HasIndex(x => x.KnowledgeArticleId);
            b.HasIndex(x => x.CreatedAt);
            b.HasOne(x => x.KnowledgeArticle)
                .WithMany(a => a.Comments)
                .HasForeignKey(x => x.KnowledgeArticleId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.User)
                .WithMany(u => u.ArticleComments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SupportTicket>(b =>
        {
            b.HasKey(x => x.SupportTicketId);
            b.Property(x => x.TicketCode).HasMaxLength(40).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            b.Property(x => x.ResolutionSummary).HasMaxLength(4000);
            b.HasIndex(x => x.TicketCode).IsUnique();
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => x.CreatedByUserId);
            b.HasIndex(x => x.AssignedAdminUserId);
            b.HasOne(x => x.CreatedByUser)
                .WithMany(u => u.CreatedSupportTickets)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.AssignedAdminUser)
                .WithMany(u => u.AssignedSupportTickets)
                .HasForeignKey(x => x.AssignedAdminUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SupportTicketMessage>(b =>
        {
            b.HasKey(x => x.SupportTicketMessageId);
            b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            b.Property(x => x.IsFromAdmin).HasDefaultValue(false);
            b.Property(x => x.AttachmentUrl).HasMaxLength(500);
            b.Property(x => x.AttachmentContentType).HasMaxLength(100);
            b.HasIndex(x => x.SupportTicketId);
            b.HasIndex(x => x.CreatedAt);
            b.HasOne(x => x.SupportTicket)
                .WithMany(t => t.Messages)
                .HasForeignKey(x => x.SupportTicketId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.SenderUser)
                .WithMany(u => u.SupportTicketMessages)
                .HasForeignKey(x => x.SenderUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AdminNotification>(b =>
        {
            b.HasKey(x => x.AdminNotificationId);
            b.Property(x => x.Type).HasMaxLength(80).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Body).HasMaxLength(1000).IsRequired();
            b.HasIndex(x => x.AdminUserId);
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => new { x.AdminUserId, x.IsRead });
            b.HasOne(x => x.AdminUser)
                .WithMany(u => u.AdminNotifications)
                .HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.SupportTicket)
                .WithMany()
                .HasForeignKey(x => x.SupportTicketId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PaymentOrder>(b =>
        {
            b.HasKey(x => x.PaymentOrderId);
            b.Property(x => x.MerchantOrderId).HasMaxLength(80).IsRequired();
            b.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            b.Property(x => x.Status).HasMaxLength(40).IsRequired();
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            b.Property(x => x.AmountUsd).HasPrecision(18, 2);
            b.Property(x => x.AmountEgp).HasPrecision(18, 2);
            b.Property(x => x.ExchangeRateUsdToEgp).HasPrecision(18, 4);
            b.Property(x => x.PaymobIntentionId).HasMaxLength(100);
            b.Property(x => x.PaymobOrderId).HasMaxLength(100);
            b.Property(x => x.PaymobTransactionId).HasMaxLength(100);
            b.Property(x => x.CheckoutUrl).HasMaxLength(1000);
            b.Property(x => x.FailureReason).HasMaxLength(500);
            b.HasIndex(x => x.MerchantOrderId).IsUnique();
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CreatedAt);
            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.Plan)
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<TelegramConversation>(b =>
        {
            b.HasKey(x => x.TelegramConversationId);
            b.Property(x => x.State).HasMaxLength(80).IsRequired();
            b.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            b.HasIndex(x => x.ChatId).IsUnique();
            b.HasOne(x => x.LinkedUser)
                .WithMany()
                .HasForeignKey(x => x.LinkedUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserFeedback>(b =>
        {
            b.HasKey(x => x.UserFeedbackId);
            b.Property(x => x.Source).HasMaxLength(40).IsRequired();
            b.Property(x => x.Category).HasMaxLength(80).IsRequired();
            b.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            b.Property(x => x.Status).HasMaxLength(40).IsRequired();
            b.Property(x => x.Rating).HasColumnType("tinyint");
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => x.Status);
            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            b.HasOne(x => x.ReviewedByUser)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

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

        modelBuilder.Entity<SystemErrorLog>(b =>
        {
            b.HasKey(e => e.SystemErrorLogId);
            b.Property(e => e.TraceId).HasMaxLength(100).IsRequired();
            b.Property(e => e.HttpMethod).HasMaxLength(20).IsRequired();
            b.Property(e => e.Path).HasMaxLength(500).IsRequired();
            b.Property(e => e.QueryString).HasMaxLength(1000);
            b.Property(e => e.Action).HasMaxLength(150).IsRequired();
            b.Property(e => e.ErrorCode).HasMaxLength(100).IsRequired();
            b.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            b.Property(e => e.ExceptionType).HasMaxLength(300);
            b.Property(e => e.ExceptionMessage).HasMaxLength(4000);
            b.Property(e => e.StackTrace).HasMaxLength(8000);
            b.Property(e => e.ResponseJson).HasMaxLength(8000);
            b.Property(e => e.IpAddress).HasMaxLength(100);
            b.Property(e => e.UserAgent).HasMaxLength(500);

            b.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasIndex(e => e.CreatedAt);
            b.HasIndex(e => e.StatusCode);
            b.HasIndex(e => e.ErrorCode);
            b.HasIndex(e => e.TraceId);
            b.HasIndex(e => e.UserId);
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
            b.Property(p => p.SaleDiscountPercent).HasPrecision(5, 2);
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

        modelBuilder.Entity<CreditActionCost>(b =>
        {
            b.HasKey(c => c.ActionKey);
            b.Property(c => c.ActionKey).HasMaxLength(50);
            b.Property(c => c.DisplayName).HasMaxLength(100).IsRequired();
            b.HasIndex(c => c.SortOrder);
        });

        var seedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<CreditActionCost>().HasData(
            new CreditActionCost
            {
                ActionKey = "Debugging",
                DisplayName = "AI Debug",
                Credits = 5,
                ShowOnPlansPage = true,
                SortOrder = 1,
                UpdatedAt = seedAt
            },
            new CreditActionCost
            {
                ActionKey = "KnowledgeAdaptation",
                DisplayName = "Knowledge reuse",
                Credits = 2,
                ShowOnPlansPage = true,
                SortOrder = 2,
                UpdatedAt = seedAt
            },
            new CreditActionCost
            {
                ActionKey = "ChallengeEvaluation",
                DisplayName = "Challenge evaluate",
                Credits = 5,
                ShowOnPlansPage = true,
                SortOrder = 3,
                UpdatedAt = seedAt
            },
            new CreditActionCost
            {
                ActionKey = "CodeReview",
                DisplayName = "Code Review",
                Credits = 15,
                ShowOnPlansPage = true,
                SortOrder = 4,
                UpdatedAt = seedAt
            },
            new CreditActionCost
            {
                ActionKey = "ChallengeGeneration",
                DisplayName = "Challenge generation",
                Credits = 3,
                ShowOnPlansPage = false,
                SortOrder = 5,
                UpdatedAt = seedAt
            }
        );

        modelBuilder.Entity<Plan>().HasData(
            new Plan
            {
                PlanId = 1,
                Name = "Free",
                Code = "FREE",
                Description = "Debug with AI, practice challenges, and publish knowledge. 100 credits / month.",
                Status = "Active",
                IsDefault = true,
                IsPublic = true,
                PeriodType = "Monthly",
                CreditsPerPeriod = 100,
                Price = 0.00m,
                Currency = "USD",
                SaleEnabled = false,
                SaleDiscountPercent = 0m,
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new Plan
            {
                PlanId = 2,
                Name = "Pro",
                Code = "PRO",
                Description = "Higher AI credits plus GitHub Code Review for teams that ship faster.",
                Status = "Active",
                IsDefault = false,
                IsPublic = true,
                PeriodType = "Monthly",
                CreditsPerPeriod = 1000,
                Price = 20.00m,
                Currency = "USD",
                SaleEnabled = false,
                SaleDiscountPercent = 0m,
                CreatedAt = seedAt,
                UpdatedAt = seedAt
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
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new Feature
            {
                FeatureId = 2,
                Key = "monthly_ai_credits",
                Name = "Monthly AI Credits",
                Description = "Total AI credits available per billing cycle.",
                Category = "Quota",
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new Feature
            {
                FeatureId = 3,
                Key = "github_code_review",
                Name = "GitHub Code Review",
                Description = "AI code review directly from connected GitHub repositories.",
                Category = "AI",
                CreatedAt = seedAt,
                UpdatedAt = seedAt
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
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new PlanFeature
            {
                PlanId = 1,
                FeatureId = 2,
                IsEnabled = true,
                LimitValue = 100,
                ConfigJson = "{\"credits\": 100}",
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new PlanFeature
            {
                PlanId = 1,
                FeatureId = 3,
                IsEnabled = false,
                LimitValue = 0,
                ConfigJson = "{\"enabled\": false}",
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new PlanFeature
            {
                PlanId = 2,
                FeatureId = 1,
                IsEnabled = true,
                LimitValue = 1000,
                ConfigJson = "{\"enabled\": true}",
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new PlanFeature
            {
                PlanId = 2,
                FeatureId = 2,
                IsEnabled = true,
                LimitValue = 1000,
                ConfigJson = "{\"credits\": 1000}",
                CreatedAt = seedAt,
                UpdatedAt = seedAt
            },
            new PlanFeature
            {
                PlanId = 2,
                FeatureId = 3,
                IsEnabled = true,
                LimitValue = null,
                ConfigJson = "{\"enabled\": true}",
                CreatedAt = seedAt,
                UpdatedAt = seedAt
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
