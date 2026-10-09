using System;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    [DbContext(typeof(DevPilotDbContext))]
    [Migration("20261009140718_AddPlanAndUsageEntities")]
    partial class AddPlanAndUsageEntities
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIModel", b =>
                {
                    b.Property<int>("AIModelId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("AIModelId"));

                    b.Property<int>("AIProviderId")
                        .HasColumnType("int");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<int>("MaxContextTokens")
                        .HasColumnType("int");

                    b.Property<string>("ModelName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("AIModelId");

                    b.HasIndex("AIProviderId");

                    b.ToTable("AIModels");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIProvider", b =>
                {
                    b.Property<int>("AIProviderId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("AIProviderId"));

                    b.Property<string>("ApiKeyConfigName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("BaseUrl")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("LastError")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("Priority")
                        .HasColumnType("int");

                    b.Property<DateTime?>("RateLimitedUntil")
                        .HasColumnType("datetime2");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.HasKey("AIProviderId");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("AIProviders");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIRequest", b =>
                {
                    b.Property<int>("AIRequestId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("AIRequestId"));

                    b.Property<int>("AIModelId")
                        .HasColumnType("int");

                    b.Property<int>("AIProviderId")
                        .HasColumnType("int");

                    b.Property<int>("CompletionTokensUsed")
                        .HasColumnType("int");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int?>("DebuggingAttemptId")
                        .HasColumnType("int");

                    b.Property<long>("DurationMs")
                        .HasColumnType("bigint");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("PromptTokensUsed")
                        .HasColumnType("int");

                    b.Property<bool>("Success")
                        .HasColumnType("bit");

                    b.Property<string>("TaskType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TotalTokensUsed")
                        .HasColumnType("int");

                    b.Property<int?>("UserId")
                        .HasColumnType("int");

                    b.HasKey("AIRequestId");

                    b.HasIndex("AIModelId");

                    b.HasIndex("AIProviderId");

                    b.HasIndex("DebuggingAttemptId");

                    b.HasIndex("UserId");

                    b.ToTable("AIRequests");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIUsageEvent", b =>
                {
                    b.Property<long>("AIUsageEventId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("AIUsageEventId"));

                    b.Property<int>("AIRequestId")
                        .HasColumnType("int");

                    b.Property<decimal?>("ActualCost")
                        .HasPrecision(18, 6)
                        .HasColumnType("decimal(18,6)");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("CorrelationId")
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<string>("CostType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int?>("CreditsCharged")
                        .HasColumnType("int");

                    b.Property<int?>("CreditsRefunded")
                        .HasColumnType("int");

                    b.Property<int?>("CreditsReserved")
                        .HasColumnType("int");

                    b.Property<long?>("DurationMs")
                        .HasColumnType("bigint");

                    b.Property<string>("ErrorCode")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<decimal?>("EstimatedCost")
                        .HasPrecision(18, 6)
                        .HasColumnType("decimal(18,6)");

                    b.Property<string>("IdempotencyKey")
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<int?>("InputTokens")
                        .HasColumnType("int");

                    b.Property<int?>("ModelId")
                        .HasColumnType("int");

                    b.Property<string>("OperationType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int?>("OutputTokens")
                        .HasColumnType("int");

                    b.Property<int?>("ProblemId")
                        .HasColumnType("int");

                    b.Property<int>("ProviderId")
                        .HasColumnType("int");

                    b.Property<string>("ProviderRequestId")
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<int>("RetrySequence")
                        .HasColumnType("int");

                    b.Property<DateTime>("StartedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<int?>("TotalTokens")
                        .HasColumnType("int");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("AIUsageEventId");

                    b.HasIndex("AIRequestId");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("ModelId");

                    b.HasIndex("OperationType");

                    b.HasIndex("ProblemId");

                    b.HasIndex("ProviderId");

                    b.HasIndex("UserId");

                    b.ToTable("AIUsageEvents");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ArticleVersion", b =>
                {
                    b.Property<int>("ArticleVersionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ArticleVersionId"));

                    b.Property<string>("ChangeReason")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ContentMarkdown")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("KnowledgeArticleId")
                        .HasColumnType("int");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("VersionNumber")
                        .HasColumnType("int");

                    b.HasKey("ArticleVersionId");

                    b.HasIndex("KnowledgeArticleId");

                    b.ToTable("ArticleVersions");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AuditLog", b =>
                {
                    b.Property<long>("AuditLogId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("AuditLogId"));

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("EntityId")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("EntityName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("IpAddress")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("NewValues")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("OldValues")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RequestId")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<int?>("UserId")
                        .HasColumnType("int");

                    b.HasKey("AuditLogId");

                    b.HasIndex("Action");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("RequestId");

                    b.HasIndex("UserId");

                    b.ToTable("AuditLogs");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Challenge", b =>
                {
                    b.Property<int>("ChallengeId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ChallengeId"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Difficulty")
                        .HasColumnType("int");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<int?>("KnowledgeArticleId")
                        .HasColumnType("int");

                    b.Property<int>("ProblemId")
                        .HasColumnType("int");

                    b.Property<string>("Requirements")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("StarterCode")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ChallengeId");

                    b.HasIndex("KnowledgeArticleId");

                    b.HasIndex("ProblemId");

                    b.ToTable("Challenges");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ChallengeAttempt", b =>
                {
                    b.Property<int>("ChallengeAttemptId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ChallengeAttemptId"));

                    b.Property<int>("ChallengeId")
                        .HasColumnType("int");

                    b.Property<string>("GitHubRepoUrl")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("StartedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("SubmittedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("ChallengeAttemptId");

                    b.HasIndex("ChallengeId");

                    b.HasIndex("UserId");

                    b.ToTable("ChallengeAttempts");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ChallengeEvaluation", b =>
                {
                    b.Property<int>("ChallengeEvaluationId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ChallengeEvaluationId"));

                    b.Property<int>("ChallengeAttemptId")
                        .HasColumnType("int");

                    b.Property<int>("CodeQualityScore")
                        .HasColumnType("int");

                    b.Property<int>("CorrectnessScore")
                        .HasColumnType("int");

                    b.Property<string>("DetailedReport")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("EvaluatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("FeedbackSummary")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("SecurityScore")
                        .HasColumnType("int");

                    b.HasKey("ChallengeEvaluationId");

                    b.HasIndex("ChallengeAttemptId")
                        .IsUnique();

                    b.ToTable("ChallengeEvaluations");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.CodeReview", b =>
                {
                    b.Property<int>("CodeReviewId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("CodeReviewId"));

                    b.Property<string>("BranchName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<int?>("GitHubRepositoryId")
                        .HasColumnType("int");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("ReviewSummary")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("ReviewedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("TargetTargetRef")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("CodeReviewId");

                    b.HasIndex("GitHubRepositoryId");

                    b.HasIndex("UserId");

                    b.ToTable("CodeReviews");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.CodeReviewFinding", b =>
                {
                    b.Property<int>("CodeReviewFindingId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("CodeReviewFindingId"));

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("CodeReviewId")
                        .HasColumnType("int");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("LineNumber")
                        .HasColumnType("int");

                    b.Property<int>("Severity")
                        .HasColumnType("int");

                    b.Property<string>("SuggestedFix")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("CodeReviewFindingId");

                    b.HasIndex("CodeReviewId");

                    b.ToTable("CodeReviewFindings");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.DebuggingAttempt", b =>
                {
                    b.Property<int>("DebuggingAttemptId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("DebuggingAttemptId"));

                    b.Property<int>("AttemptNumber")
                        .HasColumnType("int");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Explanation")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("ProblemId")
                        .HasColumnType("int");

                    b.Property<int>("ProblemRequestId")
                        .HasColumnType("int");

                    b.Property<string>("ProposedSolution")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RootCauseAnalysis")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("DebuggingAttemptId");

                    b.HasIndex("ProblemId");

                    b.HasIndex("ProblemRequestId");

                    b.ToTable("DebuggingAttempts");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.DeveloperFeedback", b =>
                {
                    b.Property<int>("DeveloperFeedbackId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("DeveloperFeedbackId"));

                    b.Property<string>("AdditionalComments")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("DebuggingAttemptId")
                        .HasColumnType("int");

                    b.Property<int>("FeedbackResult")
                        .HasColumnType("int");

                    b.HasKey("DeveloperFeedbackId");

                    b.HasIndex("DebuggingAttemptId");

                    b.ToTable("DeveloperFeedbacks");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.EmailVerification", b =>
                {
                    b.Property<int>("VerificationId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("VerificationId"));

                    b.Property<int>("Attempts")
                        .HasColumnType("int");

                    b.Property<string>("CodeHash")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("ExpiresAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Method")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("TokenHash")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.Property<DateTime?>("VerifiedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("VerificationId");

                    b.HasIndex("UserId");

                    b.ToTable("EmailVerifications");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Feature", b =>
                {
                    b.Property<int>("FeatureId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("FeatureId"));

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("FeatureId");

                    b.HasIndex("Key")
                        .IsUnique();

                    b.ToTable("Features");

                    b.HasData(
                        new
                        {
                            FeatureId = 1,
                            Category = "AI",
                            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                            Description = "Access to AI debugging assistant.",
                            Key = "ai_debugging",
                            Name = "AI Debugging",
                            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                        },
                        new
                        {
                            FeatureId = 2,
                            Category = "Quota",
                            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                            Description = "Total AI credits available per billing cycle.",
                            Key = "monthly_ai_credits",
                            Name = "Monthly AI Credits",
                            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                        });
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.GitHubAccount", b =>
                {
                    b.Property<int>("GitHubAccountId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("GitHubAccountId"));

                    b.Property<string>("AccessTokenEncrypted")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("ConnectedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("GitHubUserId")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("GitHubUsername")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("GitHubAccountId");

                    b.HasIndex("UserId");

                    b.ToTable("GitHubAccounts");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.GitHubRepository", b =>
                {
                    b.Property<int>("GitHubRepositoryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("GitHubRepositoryId"));

                    b.Property<string>("DefaultBranch")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("GitHubAccountId")
                        .HasColumnType("int");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsPrivate")
                        .HasColumnType("bit");

                    b.Property<string>("RepoName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RepoUrl")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("GitHubRepositoryId");

                    b.HasIndex("GitHubAccountId");

                    b.ToTable("GitHubRepositories");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.KnowledgeArticle", b =>
                {
                    b.Property<int>("KnowledgeArticleId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("KnowledgeArticleId"));

                    b.Property<string>("ArticleCode")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("CurrentVersion")
                        .HasColumnType("int");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsPublished")
                        .HasColumnType("bit");

                    b.Property<int>("ProblemId")
                        .HasColumnType("int");

                    b.Property<string>("Summary")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("KnowledgeArticleId");

                    b.HasIndex("ProblemId");

                    b.ToTable("KnowledgeArticles");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Plan", b =>
                {
                    b.Property<int>("PlanId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("PlanId"));

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("CreditsPerPeriod")
                        .HasColumnType("int");

                    b.Property<string>("Currency")
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("IsDefault")
                        .HasColumnType("bit");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("PeriodType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<decimal?>("Price")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("PlanId");

                    b.HasIndex("Code")
                        .IsUnique();

                    b.ToTable("Plans");

                    b.HasData(
                        new
                        {
                            PlanId = 1,
                            Code = "FREE",
                            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                            CreditsPerPeriod = 100,
                            Currency = "USD",
                            Description = "Default Free tier with standard AI quota.",
                            IsDefault = true,
                            IsPublic = true,
                            Name = "Free",
                            PeriodType = "Monthly",
                            Price = 0.00m,
                            Status = "Active",
                            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                        });
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.PlanFeature", b =>
                {
                    b.Property<int>("PlanId")
                        .HasColumnType("int");

                    b.Property<int>("FeatureId")
                        .HasColumnType("int");

                    b.Property<string>("ConfigJson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("bit");

                    b.Property<int?>("LimitValue")
                        .HasColumnType("int");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("PlanId", "FeatureId");

                    b.HasIndex("FeatureId");

                    b.ToTable("PlanFeatures");

                    b.HasData(
                        new
                        {
                            PlanId = 1,
                            FeatureId = 1,
                            ConfigJson = "{\"enabled\": true}",
                            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                            IsEnabled = true,
                            LimitValue = 100,
                            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                        },
                        new
                        {
                            PlanId = 1,
                            FeatureId = 2,
                            ConfigJson = "{\"credits\": 100}",
                            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                            IsEnabled = true,
                            LimitValue = 100,
                            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                        });
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Problem", b =>
                {
                    b.Property<int>("ProblemId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ProblemId"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("ProblemCode")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTime?>("ResolvedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<string>("Summary")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("ProblemId");

                    b.HasIndex("ProblemCode")
                        .IsUnique();

                    b.HasIndex("UserId");

                    b.ToTable("Problems");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ProblemAttachment", b =>
                {
                    b.Property<int>("ProblemAttachmentId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ProblemAttachmentId"));

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FileType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<int>("ProblemRequestId")
                        .HasColumnType("int");

                    b.Property<DateTime>("UploadedAt")
                        .HasColumnType("datetime2");

                    b.HasKey("ProblemAttachmentId");

                    b.HasIndex("ProblemRequestId");

                    b.ToTable("ProblemAttachments");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ProblemRequest", b =>
                {
                    b.Property<int>("ProblemRequestId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ProblemRequestId"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Framework")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Logs")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("ProblemId")
                        .HasColumnType("int");

                    b.Property<string>("SourceCode")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("StackTrace")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ProblemRequestId");

                    b.HasIndex("ProblemId");

                    b.ToTable("ProblemRequests");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ProblemSolution", b =>
                {
                    b.Property<int>("ProblemSolutionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("ProblemSolutionId"));

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("DetailedSteps")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<int>("ProblemId")
                        .HasColumnType("int");

                    b.Property<string>("RootCause")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("SolvedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("SuccessfulAttemptId")
                        .HasColumnType("int");

                    b.Property<string>("Summary")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("ProblemSolutionId");

                    b.HasIndex("ProblemId");

                    b.HasIndex("SuccessfulAttemptId");

                    b.ToTable("ProblemSolutions");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.RefreshToken", b =>
                {
                    b.Property<int>("RefreshTokenId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("RefreshTokenId"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("ExpiresAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsRevoked")
                        .HasColumnType("bit");

                    b.Property<bool>("IsUsed")
                        .HasColumnType("bit");

                    b.Property<string>("ReplacedByTokenHash")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("RevokedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("RefreshTokenId");

                    b.HasIndex("TokenHash");

                    b.HasIndex("UserId");

                    b.ToTable("RefreshTokens");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Role", b =>
                {
                    b.Property<int>("RoleId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("RoleId"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("RoleId");

                    b.ToTable("Roles");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UsageLedger", b =>
                {
                    b.Property<long>("UsageLedgerId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("UsageLedgerId"));

                    b.Property<long?>("AIUsageEventId")
                        .HasColumnType("bigint");

                    b.Property<int>("Amount")
                        .HasColumnType("int");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("EntryType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("MetadataJson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Reason")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<string>("ReferenceId")
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<long>("UsagePeriodId")
                        .HasColumnType("bigint");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("UsageLedgerId");

                    b.HasIndex("AIUsageEventId");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("UsagePeriodId");

                    b.HasIndex("UserId");

                    b.ToTable("UsageLedgers");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UsagePeriod", b =>
                {
                    b.Property<long>("UsagePeriodId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("UsagePeriodId"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("CreditsAllocated")
                        .HasColumnType("int");

                    b.Property<int>("CreditsRefunded")
                        .HasColumnType("int");

                    b.Property<int>("CreditsReserved")
                        .HasColumnType("int");

                    b.Property<int>("CreditsUsed")
                        .HasColumnType("int");

                    b.Property<DateTime>("PeriodEnd")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("PeriodStart")
                        .HasColumnType("datetime2");

                    b.Property<int>("PlanId")
                        .HasColumnType("int");

                    b.Property<long>("RequestCount")
                        .HasColumnType("bigint");

                    b.Property<long>("TokenCount")
                        .HasColumnType("bigint");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("UsagePeriodId");

                    b.HasIndex("PlanId");

                    b.HasIndex("UserId");

                    b.HasIndex("UserId", "PeriodStart", "PeriodEnd")
                        .IsUnique();

                    b.ToTable("UsagePeriods");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.User", b =>
                {
                    b.Property<int>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("UserId"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("datetime2");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTime?>("EmailVerifiedAt")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UserRole", b =>
                {
                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.Property<int>("RoleId")
                        .HasColumnType("int");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("UserRoles");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UserSubscription", b =>
                {
                    b.Property<long>("SubscriptionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("SubscriptionId"));

                    b.Property<int?>("AssignedByUserId")
                        .HasColumnType("int");

                    b.Property<string>("AssignmentSource")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("CancellationReason")
                        .HasMaxLength(255)
                        .HasColumnType("nvarchar(255)");

                    b.Property<DateTime?>("CancelledAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("datetime2");

                    b.Property<int>("PlanId")
                        .HasColumnType("int");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("datetime2");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("datetime2");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("SubscriptionId");

                    b.HasIndex("AssignedByUserId");

                    b.HasIndex("PlanId");

                    b.HasIndex("Status");

                    b.HasIndex("UserId");

                    b.ToTable("UserSubscriptions");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIModel", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.AIProvider", "Provider")
                        .WithMany("Models")
                        .HasForeignKey("AIProviderId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Provider");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIRequest", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.AIModel", "Model")
                        .WithMany("Requests")
                        .HasForeignKey("AIModelId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.AIProvider", "Provider")
                        .WithMany("Requests")
                        .HasForeignKey("AIProviderId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.DebuggingAttempt", "DebuggingAttempt")
                        .WithMany("AIRequests")
                        .HasForeignKey("DebuggingAttemptId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("DebuggingAttempt");

                    b.Navigation("Model");

                    b.Navigation("Provider");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIUsageEvent", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.AIRequest", "AIRequest")
                        .WithMany("AIUsageEvents")
                        .HasForeignKey("AIRequestId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.AIModel", "Model")
                        .WithMany("AIUsageEvents")
                        .HasForeignKey("ModelId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("DevPilotAI.Domain.Entities.Problem", "Problem")
                        .WithMany("AIUsageEvents")
                        .HasForeignKey("ProblemId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("DevPilotAI.Domain.Entities.AIProvider", "Provider")
                        .WithMany("AIUsageEvents")
                        .HasForeignKey("ProviderId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("AIUsageEvents")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("AIRequest");

                    b.Navigation("Model");

                    b.Navigation("Problem");

                    b.Navigation("Provider");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ArticleVersion", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.KnowledgeArticle", "KnowledgeArticle")
                        .WithMany("Versions")
                        .HasForeignKey("KnowledgeArticleId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("KnowledgeArticle");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AuditLog", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("AuditLogs")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Challenge", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.KnowledgeArticle", "KnowledgeArticle")
                        .WithMany("Challenges")
                        .HasForeignKey("KnowledgeArticleId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("DevPilotAI.Domain.Entities.Problem", "Problem")
                        .WithMany("Challenges")
                        .HasForeignKey("ProblemId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("KnowledgeArticle");

                    b.Navigation("Problem");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ChallengeAttempt", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Challenge", "Challenge")
                        .WithMany("Attempts")
                        .HasForeignKey("ChallengeId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("ChallengeAttempts")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Challenge");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ChallengeEvaluation", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.ChallengeAttempt", "ChallengeAttempt")
                        .WithOne("Evaluation")
                        .HasForeignKey("DevPilotAI.Domain.Entities.ChallengeEvaluation", "ChallengeAttemptId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("ChallengeAttempt");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.CodeReview", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.GitHubRepository", "GitHubRepository")
                        .WithMany("CodeReviews")
                        .HasForeignKey("GitHubRepositoryId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("CodeReviews")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("GitHubRepository");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.CodeReviewFinding", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.CodeReview", "CodeReview")
                        .WithMany("Findings")
                        .HasForeignKey("CodeReviewId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("CodeReview");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.DebuggingAttempt", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Problem", "Problem")
                        .WithMany("DebuggingAttempts")
                        .HasForeignKey("ProblemId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.ProblemRequest", "ProblemRequest")
                        .WithMany("DebuggingAttempts")
                        .HasForeignKey("ProblemRequestId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Problem");

                    b.Navigation("ProblemRequest");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.DeveloperFeedback", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.DebuggingAttempt", "DebuggingAttempt")
                        .WithMany("Feedbacks")
                        .HasForeignKey("DebuggingAttemptId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("DebuggingAttempt");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.EmailVerification", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.GitHubAccount", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("GitHubAccounts")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.GitHubRepository", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.GitHubAccount", "GitHubAccount")
                        .WithMany("Repositories")
                        .HasForeignKey("GitHubAccountId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("GitHubAccount");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.KnowledgeArticle", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Problem", "Problem")
                        .WithMany("KnowledgeArticles")
                        .HasForeignKey("ProblemId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Problem");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.PlanFeature", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Feature", "Feature")
                        .WithMany("PlanFeatures")
                        .HasForeignKey("FeatureId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.Plan", "Plan")
                        .WithMany("PlanFeatures")
                        .HasForeignKey("PlanId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Feature");

                    b.Navigation("Plan");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Problem", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("Problems")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ProblemAttachment", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.ProblemRequest", "ProblemRequest")
                        .WithMany("Attachments")
                        .HasForeignKey("ProblemRequestId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("ProblemRequest");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ProblemRequest", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Problem", "Problem")
                        .WithMany("ProblemRequests")
                        .HasForeignKey("ProblemId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Problem");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ProblemSolution", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Problem", "Problem")
                        .WithMany("ProblemSolutions")
                        .HasForeignKey("ProblemId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.DebuggingAttempt", "SuccessfulAttempt")
                        .WithMany()
                        .HasForeignKey("SuccessfulAttemptId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Problem");

                    b.Navigation("SuccessfulAttempt");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.RefreshToken", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UsageLedger", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.AIUsageEvent", "AIUsageEvent")
                        .WithMany("UsageLedgers")
                        .HasForeignKey("AIUsageEventId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("DevPilotAI.Domain.Entities.UsagePeriod", "UsagePeriod")
                        .WithMany("UsageLedgers")
                        .HasForeignKey("UsagePeriodId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("UsageLedgers")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("AIUsageEvent");

                    b.Navigation("UsagePeriod");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UsagePeriod", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Plan", "Plan")
                        .WithMany("UsagePeriods")
                        .HasForeignKey("PlanId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("UsagePeriods")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Plan");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UserRole", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.Role", "Role")
                        .WithMany("UserRoles")
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("UserRoles")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Role");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UserSubscription", b =>
                {
                    b.HasOne("DevPilotAI.Domain.Entities.User", "AssignedByUser")
                        .WithMany()
                        .HasForeignKey("AssignedByUserId")
                        .OnDelete(DeleteBehavior.NoAction);

                    b.HasOne("DevPilotAI.Domain.Entities.Plan", "Plan")
                        .WithMany("UserSubscriptions")
                        .HasForeignKey("PlanId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("DevPilotAI.Domain.Entities.User", "User")
                        .WithMany("UserSubscriptions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("AssignedByUser");

                    b.Navigation("Plan");

                    b.Navigation("User");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIModel", b =>
                {
                    b.Navigation("AIUsageEvents");

                    b.Navigation("Requests");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIProvider", b =>
                {
                    b.Navigation("AIUsageEvents");

                    b.Navigation("Models");

                    b.Navigation("Requests");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIRequest", b =>
                {
                    b.Navigation("AIUsageEvents");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.AIUsageEvent", b =>
                {
                    b.Navigation("UsageLedgers");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Challenge", b =>
                {
                    b.Navigation("Attempts");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ChallengeAttempt", b =>
                {
                    b.Navigation("Evaluation");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.CodeReview", b =>
                {
                    b.Navigation("Findings");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.DebuggingAttempt", b =>
                {
                    b.Navigation("AIRequests");

                    b.Navigation("Feedbacks");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Feature", b =>
                {
                    b.Navigation("PlanFeatures");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.GitHubAccount", b =>
                {
                    b.Navigation("Repositories");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.GitHubRepository", b =>
                {
                    b.Navigation("CodeReviews");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.KnowledgeArticle", b =>
                {
                    b.Navigation("Challenges");

                    b.Navigation("Versions");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Plan", b =>
                {
                    b.Navigation("PlanFeatures");

                    b.Navigation("UsagePeriods");

                    b.Navigation("UserSubscriptions");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Problem", b =>
                {
                    b.Navigation("AIUsageEvents");

                    b.Navigation("Challenges");

                    b.Navigation("DebuggingAttempts");

                    b.Navigation("KnowledgeArticles");

                    b.Navigation("ProblemRequests");

                    b.Navigation("ProblemSolutions");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.ProblemRequest", b =>
                {
                    b.Navigation("Attachments");

                    b.Navigation("DebuggingAttempts");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.Role", b =>
                {
                    b.Navigation("UserRoles");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.UsagePeriod", b =>
                {
                    b.Navigation("UsageLedgers");
                });

            modelBuilder.Entity("DevPilotAI.Domain.Entities.User", b =>
                {
                    b.Navigation("AIUsageEvents");

                    b.Navigation("AuditLogs");

                    b.Navigation("ChallengeAttempts");

                    b.Navigation("CodeReviews");

                    b.Navigation("GitHubAccounts");

                    b.Navigation("Problems");

                    b.Navigation("UsageLedgers");

                    b.Navigation("UsagePeriods");

                    b.Navigation("UserRoles");

                    b.Navigation("UserSubscriptions");
                });
#pragma warning restore 612, 618
        }
    }
}
