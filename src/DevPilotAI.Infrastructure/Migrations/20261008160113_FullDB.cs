using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevPilotAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FullDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIProviders",
                columns: table => new
                {
                    AIProviderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKeyConfigName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RateLimitedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIProviders", x => x.AIProviderId);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    AIModelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AIProviderId = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxContextTokens = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModels", x => x.AIModelId);
                    table.ForeignKey(
                        name: "FK_AIModels_AIProviders_AIProviderId",
                        column: x => x.AIProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "AIProviderId");
                });

            migrationBuilder.CreateTable(
                name: "EmailVerifications",
                columns: table => new
                {
                    VerificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerifications", x => x.VerificationId);
                    table.ForeignKey(
                        name: "FK_EmailVerifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GitHubAccounts",
                columns: table => new
                {
                    GitHubAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GitHubUsername = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GitHubUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubAccounts", x => x.GitHubAccountId);
                    table.ForeignKey(
                        name: "FK_GitHubAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Problems",
                columns: table => new
                {
                    ProblemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problems", x => x.ProblemId);
                    table.ForeignKey(
                        name: "FK_Problems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    RefreshTokenId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsUsed = table.Column<bool>(type: "bit", nullable: false),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.RefreshTokenId);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId");
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "GitHubRepositories",
                columns: table => new
                {
                    GitHubRepositoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GitHubAccountId = table.Column<int>(type: "int", nullable: false),
                    RepoName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RepoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultBranch = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubRepositories", x => x.GitHubRepositoryId);
                    table.ForeignKey(
                        name: "FK_GitHubRepositories_GitHubAccounts_GitHubAccountId",
                        column: x => x.GitHubAccountId,
                        principalTable: "GitHubAccounts",
                        principalColumn: "GitHubAccountId");
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeArticles",
                columns: table => new
                {
                    KnowledgeArticleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    ArticleCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticles", x => x.KnowledgeArticleId);
                    table.ForeignKey(
                        name: "FK_KnowledgeArticles_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "ProblemId");
                });

            migrationBuilder.CreateTable(
                name: "ProblemRequests",
                columns: table => new
                {
                    ProblemRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Logs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Framework = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemRequests", x => x.ProblemRequestId);
                    table.ForeignKey(
                        name: "FK_ProblemRequests_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "ProblemId");
                });

            migrationBuilder.CreateTable(
                name: "CodeReviews",
                columns: table => new
                {
                    CodeReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GitHubRepositoryId = table.Column<int>(type: "int", nullable: true),
                    BranchName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetTargetRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeReviews", x => x.CodeReviewId);
                    table.ForeignKey(
                        name: "FK_CodeReviews_GitHubRepositories_GitHubRepositoryId",
                        column: x => x.GitHubRepositoryId,
                        principalTable: "GitHubRepositories",
                        principalColumn: "GitHubRepositoryId");
                    table.ForeignKey(
                        name: "FK_CodeReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "ArticleVersions",
                columns: table => new
                {
                    ArticleVersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KnowledgeArticleId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleVersions", x => x.ArticleVersionId);
                    table.ForeignKey(
                        name: "FK_ArticleVersions_KnowledgeArticles_KnowledgeArticleId",
                        column: x => x.KnowledgeArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "KnowledgeArticleId");
                });

            migrationBuilder.CreateTable(
                name: "Challenges",
                columns: table => new
                {
                    ChallengeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    KnowledgeArticleId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Requirements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StarterCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Challenges", x => x.ChallengeId);
                    table.ForeignKey(
                        name: "FK_Challenges_KnowledgeArticles_KnowledgeArticleId",
                        column: x => x.KnowledgeArticleId,
                        principalTable: "KnowledgeArticles",
                        principalColumn: "KnowledgeArticleId");
                    table.ForeignKey(
                        name: "FK_Challenges_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "ProblemId");
                });

            migrationBuilder.CreateTable(
                name: "DebuggingAttempts",
                columns: table => new
                {
                    DebuggingAttemptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    ProblemRequestId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RootCauseAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProposedSolution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebuggingAttempts", x => x.DebuggingAttemptId);
                    table.ForeignKey(
                        name: "FK_DebuggingAttempts_ProblemRequests_ProblemRequestId",
                        column: x => x.ProblemRequestId,
                        principalTable: "ProblemRequests",
                        principalColumn: "ProblemRequestId");
                    table.ForeignKey(
                        name: "FK_DebuggingAttempts_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "ProblemId");
                });

            migrationBuilder.CreateTable(
                name: "ProblemAttachments",
                columns: table => new
                {
                    ProblemAttachmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemRequestId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemAttachments", x => x.ProblemAttachmentId);
                    table.ForeignKey(
                        name: "FK_ProblemAttachments_ProblemRequests_ProblemRequestId",
                        column: x => x.ProblemRequestId,
                        principalTable: "ProblemRequests",
                        principalColumn: "ProblemRequestId");
                });

            migrationBuilder.CreateTable(
                name: "CodeReviewFindings",
                columns: table => new
                {
                    CodeReviewFindingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodeReviewId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuggestedFix = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeReviewFindings", x => x.CodeReviewFindingId);
                    table.ForeignKey(
                        name: "FK_CodeReviewFindings_CodeReviews_CodeReviewId",
                        column: x => x.CodeReviewId,
                        principalTable: "CodeReviews",
                        principalColumn: "CodeReviewId");
                });

            migrationBuilder.CreateTable(
                name: "ChallengeAttempts",
                columns: table => new
                {
                    ChallengeAttemptId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChallengeId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GitHubRepoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeAttempts", x => x.ChallengeAttemptId);
                    table.ForeignKey(
                        name: "FK_ChallengeAttempts_Challenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "Challenges",
                        principalColumn: "ChallengeId");
                    table.ForeignKey(
                        name: "FK_ChallengeAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "AIRequests",
                columns: table => new
                {
                    AIRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    AIProviderId = table.Column<int>(type: "int", nullable: false),
                    AIModelId = table.Column<int>(type: "int", nullable: false),
                    DebuggingAttemptId = table.Column<int>(type: "int", nullable: true),
                    TaskType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PromptTokensUsed = table.Column<int>(type: "int", nullable: false),
                    CompletionTokensUsed = table.Column<int>(type: "int", nullable: false),
                    TotalTokensUsed = table.Column<int>(type: "int", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIRequests", x => x.AIRequestId);
                    table.ForeignKey(
                        name: "FK_AIRequests_AIModels_AIModelId",
                        column: x => x.AIModelId,
                        principalTable: "AIModels",
                        principalColumn: "AIModelId");
                    table.ForeignKey(
                        name: "FK_AIRequests_AIProviders_AIProviderId",
                        column: x => x.AIProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "AIProviderId");
                    table.ForeignKey(
                        name: "FK_AIRequests_DebuggingAttempts_DebuggingAttemptId",
                        column: x => x.DebuggingAttemptId,
                        principalTable: "DebuggingAttempts",
                        principalColumn: "DebuggingAttemptId");
                    table.ForeignKey(
                        name: "FK_AIRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "DeveloperFeedbacks",
                columns: table => new
                {
                    DeveloperFeedbackId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DebuggingAttemptId = table.Column<int>(type: "int", nullable: false),
                    FeedbackResult = table.Column<int>(type: "int", nullable: false),
                    AdditionalComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperFeedbacks", x => x.DeveloperFeedbackId);
                    table.ForeignKey(
                        name: "FK_DeveloperFeedbacks_DebuggingAttempts_DebuggingAttemptId",
                        column: x => x.DebuggingAttemptId,
                        principalTable: "DebuggingAttempts",
                        principalColumn: "DebuggingAttemptId");
                });

            migrationBuilder.CreateTable(
                name: "ProblemSolutions",
                columns: table => new
                {
                    ProblemSolutionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    SuccessfulAttemptId = table.Column<int>(type: "int", nullable: false),
                    RootCause = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetailedSteps = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SolvedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemSolutions", x => x.ProblemSolutionId);
                    table.ForeignKey(
                        name: "FK_ProblemSolutions_DebuggingAttempts_SuccessfulAttemptId",
                        column: x => x.SuccessfulAttemptId,
                        principalTable: "DebuggingAttempts",
                        principalColumn: "DebuggingAttemptId");
                    table.ForeignKey(
                        name: "FK_ProblemSolutions_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "ProblemId");
                });

            migrationBuilder.CreateTable(
                name: "ChallengeEvaluations",
                columns: table => new
                {
                    ChallengeEvaluationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChallengeAttemptId = table.Column<int>(type: "int", nullable: false),
                    CorrectnessScore = table.Column<int>(type: "int", nullable: false),
                    CodeQualityScore = table.Column<int>(type: "int", nullable: false),
                    SecurityScore = table.Column<int>(type: "int", nullable: false),
                    FeedbackSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetailedReport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChallengeEvaluations", x => x.ChallengeEvaluationId);
                    table.ForeignKey(
                        name: "FK_ChallengeEvaluations_ChallengeAttempts_ChallengeAttemptId",
                        column: x => x.ChallengeAttemptId,
                        principalTable: "ChallengeAttempts",
                        principalColumn: "ChallengeAttemptId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIModels_AIProviderId",
                table: "AIModels",
                column: "AIProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AIProviders_Name",
                table: "AIProviders",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIRequests_AIModelId",
                table: "AIRequests",
                column: "AIModelId");

            migrationBuilder.CreateIndex(
                name: "IX_AIRequests_AIProviderId",
                table: "AIRequests",
                column: "AIProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AIRequests_DebuggingAttemptId",
                table: "AIRequests",
                column: "DebuggingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_AIRequests_UserId",
                table: "AIRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleVersions_KnowledgeArticleId",
                table: "ArticleVersions",
                column: "KnowledgeArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeAttempts_ChallengeId",
                table: "ChallengeAttempts",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeAttempts_UserId",
                table: "ChallengeAttempts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChallengeEvaluations_ChallengeAttemptId",
                table: "ChallengeEvaluations",
                column: "ChallengeAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_KnowledgeArticleId",
                table: "Challenges",
                column: "KnowledgeArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Challenges_ProblemId",
                table: "Challenges",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeReviewFindings_CodeReviewId",
                table: "CodeReviewFindings",
                column: "CodeReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeReviews_GitHubRepositoryId",
                table: "CodeReviews",
                column: "GitHubRepositoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeReviews_UserId",
                table: "CodeReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingAttempts_ProblemId",
                table: "DebuggingAttempts",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_DebuggingAttempts_ProblemRequestId",
                table: "DebuggingAttempts",
                column: "ProblemRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperFeedbacks_DebuggingAttemptId",
                table: "DeveloperFeedbacks",
                column: "DebuggingAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_UserId",
                table: "EmailVerifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubAccounts_UserId",
                table: "GitHubAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GitHubRepositories_GitHubAccountId",
                table: "GitHubRepositories",
                column: "GitHubAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticles_ProblemId",
                table: "KnowledgeArticles",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemAttachments_ProblemRequestId",
                table: "ProblemAttachments",
                column: "ProblemRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemRequests_ProblemId",
                table: "ProblemRequests",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_ProblemCode",
                table: "Problems",
                column: "ProblemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Problems_UserId",
                table: "Problems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemSolutions_ProblemId",
                table: "ProblemSolutions",
                column: "ProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemSolutions_SuccessfulAttemptId",
                table: "ProblemSolutions",
                column: "SuccessfulAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIRequests");

            migrationBuilder.DropTable(
                name: "ArticleVersions");

            migrationBuilder.DropTable(
                name: "ChallengeEvaluations");

            migrationBuilder.DropTable(
                name: "CodeReviewFindings");

            migrationBuilder.DropTable(
                name: "DeveloperFeedbacks");

            migrationBuilder.DropTable(
                name: "EmailVerifications");

            migrationBuilder.DropTable(
                name: "ProblemAttachments");

            migrationBuilder.DropTable(
                name: "ProblemSolutions");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "AIModels");

            migrationBuilder.DropTable(
                name: "ChallengeAttempts");

            migrationBuilder.DropTable(
                name: "CodeReviews");

            migrationBuilder.DropTable(
                name: "DebuggingAttempts");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "AIProviders");

            migrationBuilder.DropTable(
                name: "Challenges");

            migrationBuilder.DropTable(
                name: "GitHubRepositories");

            migrationBuilder.DropTable(
                name: "ProblemRequests");

            migrationBuilder.DropTable(
                name: "KnowledgeArticles");

            migrationBuilder.DropTable(
                name: "GitHubAccounts");

            migrationBuilder.DropTable(
                name: "Problems");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
