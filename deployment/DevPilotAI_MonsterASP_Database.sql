IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [AIProviders] (
        [AIProviderId] int NOT NULL IDENTITY,
        [Name] nvarchar(450) NOT NULL,
        [BaseUrl] nvarchar(max) NOT NULL,
        [ApiKeyConfigName] nvarchar(max) NOT NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [RateLimitedUntil] datetime2 NULL,
        [LastError] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AIProviders] PRIMARY KEY ([AIProviderId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [Roles] (
        [RoleId] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [Users] (
        [UserId] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [EmailVerifiedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [AIModels] (
        [AIModelId] int NOT NULL IDENTITY,
        [AIProviderId] int NOT NULL,
        [ModelName] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [MaxContextTokens] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AIModels] PRIMARY KEY ([AIModelId]),
        CONSTRAINT [FK_AIModels_AIProviders_AIProviderId] FOREIGN KEY ([AIProviderId]) REFERENCES [AIProviders] ([AIProviderId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [EmailVerifications] (
        [VerificationId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [CodeHash] nvarchar(max) NOT NULL,
        [TokenHash] nvarchar(max) NULL,
        [Method] nvarchar(max) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Attempts] int NOT NULL,
        [VerifiedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailVerifications] PRIMARY KEY ([VerificationId]),
        CONSTRAINT [FK_EmailVerifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [GitHubAccounts] (
        [GitHubAccountId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [GitHubUsername] nvarchar(max) NOT NULL,
        [GitHubUserId] nvarchar(max) NOT NULL,
        [AccessTokenEncrypted] nvarchar(max) NOT NULL,
        [ConnectedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GitHubAccounts] PRIMARY KEY ([GitHubAccountId]),
        CONSTRAINT [FK_GitHubAccounts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [Problems] (
        [ProblemId] int NOT NULL IDENTITY,
        [ProblemCode] nvarchar(450) NOT NULL,
        [UserId] int NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Summary] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        CONSTRAINT [PK_Problems] PRIMARY KEY ([ProblemId]),
        CONSTRAINT [FK_Problems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [RefreshTokenId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TokenHash] nvarchar(450) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        [RevokedAt] datetime2 NULL,
        [IsUsed] bit NOT NULL,
        [ReplacedByTokenHash] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([RefreshTokenId]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] int NOT NULL,
        [RoleId] int NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([RoleId]),
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [GitHubRepositories] (
        [GitHubRepositoryId] int NOT NULL IDENTITY,
        [GitHubAccountId] int NOT NULL,
        [RepoName] nvarchar(max) NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [RepoUrl] nvarchar(max) NOT NULL,
        [DefaultBranch] nvarchar(max) NOT NULL,
        [IsPrivate] bit NOT NULL,
        CONSTRAINT [PK_GitHubRepositories] PRIMARY KEY ([GitHubRepositoryId]),
        CONSTRAINT [FK_GitHubRepositories_GitHubAccounts_GitHubAccountId] FOREIGN KEY ([GitHubAccountId]) REFERENCES [GitHubAccounts] ([GitHubAccountId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [KnowledgeArticles] (
        [KnowledgeArticleId] int NOT NULL IDENTITY,
        [ProblemId] int NOT NULL,
        [ArticleCode] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Summary] nvarchar(max) NOT NULL,
        [CurrentVersion] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_KnowledgeArticles] PRIMARY KEY ([KnowledgeArticleId]),
        CONSTRAINT [FK_KnowledgeArticles_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([ProblemId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [ProblemRequests] (
        [ProblemRequestId] int NOT NULL IDENTITY,
        [ProblemId] int NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [StackTrace] nvarchar(max) NULL,
        [SourceCode] nvarchar(max) NULL,
        [Logs] nvarchar(max) NULL,
        [Framework] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProblemRequests] PRIMARY KEY ([ProblemRequestId]),
        CONSTRAINT [FK_ProblemRequests_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([ProblemId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [CodeReviews] (
        [CodeReviewId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [GitHubRepositoryId] int NULL,
        [BranchName] nvarchar(max) NOT NULL,
        [TargetTargetRef] nvarchar(max) NOT NULL,
        [ReviewSummary] nvarchar(max) NOT NULL,
        [ReviewedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CodeReviews] PRIMARY KEY ([CodeReviewId]),
        CONSTRAINT [FK_CodeReviews_GitHubRepositories_GitHubRepositoryId] FOREIGN KEY ([GitHubRepositoryId]) REFERENCES [GitHubRepositories] ([GitHubRepositoryId]),
        CONSTRAINT [FK_CodeReviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [ArticleVersions] (
        [ArticleVersionId] int NOT NULL IDENTITY,
        [KnowledgeArticleId] int NOT NULL,
        [VersionNumber] int NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [ContentMarkdown] nvarchar(max) NOT NULL,
        [ChangeReason] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ArticleVersions] PRIMARY KEY ([ArticleVersionId]),
        CONSTRAINT [FK_ArticleVersions_KnowledgeArticles_KnowledgeArticleId] FOREIGN KEY ([KnowledgeArticleId]) REFERENCES [KnowledgeArticles] ([KnowledgeArticleId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [Challenges] (
        [ChallengeId] int NOT NULL IDENTITY,
        [ProblemId] int NOT NULL,
        [KnowledgeArticleId] int NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Requirements] nvarchar(max) NOT NULL,
        [StarterCode] nvarchar(max) NOT NULL,
        [Difficulty] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Challenges] PRIMARY KEY ([ChallengeId]),
        CONSTRAINT [FK_Challenges_KnowledgeArticles_KnowledgeArticleId] FOREIGN KEY ([KnowledgeArticleId]) REFERENCES [KnowledgeArticles] ([KnowledgeArticleId]),
        CONSTRAINT [FK_Challenges_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([ProblemId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [DebuggingAttempts] (
        [DebuggingAttemptId] int NOT NULL IDENTITY,
        [ProblemId] int NOT NULL,
        [ProblemRequestId] int NOT NULL,
        [AttemptNumber] int NOT NULL,
        [RootCauseAnalysis] nvarchar(max) NOT NULL,
        [Explanation] nvarchar(max) NOT NULL,
        [ProposedSolution] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DebuggingAttempts] PRIMARY KEY ([DebuggingAttemptId]),
        CONSTRAINT [FK_DebuggingAttempts_ProblemRequests_ProblemRequestId] FOREIGN KEY ([ProblemRequestId]) REFERENCES [ProblemRequests] ([ProblemRequestId]),
        CONSTRAINT [FK_DebuggingAttempts_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([ProblemId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [ProblemAttachments] (
        [ProblemAttachmentId] int NOT NULL IDENTITY,
        [ProblemRequestId] int NOT NULL,
        [FilePath] nvarchar(max) NOT NULL,
        [FileType] nvarchar(max) NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProblemAttachments] PRIMARY KEY ([ProblemAttachmentId]),
        CONSTRAINT [FK_ProblemAttachments_ProblemRequests_ProblemRequestId] FOREIGN KEY ([ProblemRequestId]) REFERENCES [ProblemRequests] ([ProblemRequestId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [CodeReviewFindings] (
        [CodeReviewFindingId] int NOT NULL IDENTITY,
        [CodeReviewId] int NOT NULL,
        [FilePath] nvarchar(max) NOT NULL,
        [LineNumber] int NULL,
        [Severity] int NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [SuggestedFix] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_CodeReviewFindings] PRIMARY KEY ([CodeReviewFindingId]),
        CONSTRAINT [FK_CodeReviewFindings_CodeReviews_CodeReviewId] FOREIGN KEY ([CodeReviewId]) REFERENCES [CodeReviews] ([CodeReviewId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [ChallengeAttempts] (
        [ChallengeAttemptId] int NOT NULL IDENTITY,
        [ChallengeId] int NOT NULL,
        [UserId] int NOT NULL,
        [GitHubRepoUrl] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [SubmittedAt] datetime2 NULL,
        CONSTRAINT [PK_ChallengeAttempts] PRIMARY KEY ([ChallengeAttemptId]),
        CONSTRAINT [FK_ChallengeAttempts_Challenges_ChallengeId] FOREIGN KEY ([ChallengeId]) REFERENCES [Challenges] ([ChallengeId]),
        CONSTRAINT [FK_ChallengeAttempts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [AIRequests] (
        [AIRequestId] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [AIProviderId] int NOT NULL,
        [AIModelId] int NOT NULL,
        [DebuggingAttemptId] int NULL,
        [TaskType] nvarchar(max) NOT NULL,
        [PromptTokensUsed] int NOT NULL,
        [CompletionTokensUsed] int NOT NULL,
        [TotalTokensUsed] int NOT NULL,
        [DurationMs] bigint NOT NULL,
        [Success] bit NOT NULL,
        [ErrorMessage] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AIRequests] PRIMARY KEY ([AIRequestId]),
        CONSTRAINT [FK_AIRequests_AIModels_AIModelId] FOREIGN KEY ([AIModelId]) REFERENCES [AIModels] ([AIModelId]),
        CONSTRAINT [FK_AIRequests_AIProviders_AIProviderId] FOREIGN KEY ([AIProviderId]) REFERENCES [AIProviders] ([AIProviderId]),
        CONSTRAINT [FK_AIRequests_DebuggingAttempts_DebuggingAttemptId] FOREIGN KEY ([DebuggingAttemptId]) REFERENCES [DebuggingAttempts] ([DebuggingAttemptId]),
        CONSTRAINT [FK_AIRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [DeveloperFeedbacks] (
        [DeveloperFeedbackId] int NOT NULL IDENTITY,
        [DebuggingAttemptId] int NOT NULL,
        [FeedbackResult] int NOT NULL,
        [AdditionalComments] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DeveloperFeedbacks] PRIMARY KEY ([DeveloperFeedbackId]),
        CONSTRAINT [FK_DeveloperFeedbacks_DebuggingAttempts_DebuggingAttemptId] FOREIGN KEY ([DebuggingAttemptId]) REFERENCES [DebuggingAttempts] ([DebuggingAttemptId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [ProblemSolutions] (
        [ProblemSolutionId] int NOT NULL IDENTITY,
        [ProblemId] int NOT NULL,
        [SuccessfulAttemptId] int NOT NULL,
        [RootCause] nvarchar(max) NOT NULL,
        [Summary] nvarchar(max) NOT NULL,
        [DetailedSteps] nvarchar(max) NOT NULL,
        [SolvedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProblemSolutions] PRIMARY KEY ([ProblemSolutionId]),
        CONSTRAINT [FK_ProblemSolutions_DebuggingAttempts_SuccessfulAttemptId] FOREIGN KEY ([SuccessfulAttemptId]) REFERENCES [DebuggingAttempts] ([DebuggingAttemptId]),
        CONSTRAINT [FK_ProblemSolutions_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([ProblemId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE TABLE [ChallengeEvaluations] (
        [ChallengeEvaluationId] int NOT NULL IDENTITY,
        [ChallengeAttemptId] int NOT NULL,
        [CorrectnessScore] int NOT NULL,
        [CodeQualityScore] int NOT NULL,
        [SecurityScore] int NOT NULL,
        [FeedbackSummary] nvarchar(max) NOT NULL,
        [DetailedReport] nvarchar(max) NOT NULL,
        [EvaluatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChallengeEvaluations] PRIMARY KEY ([ChallengeEvaluationId]),
        CONSTRAINT [FK_ChallengeEvaluations_ChallengeAttempts_ChallengeAttemptId] FOREIGN KEY ([ChallengeAttemptId]) REFERENCES [ChallengeAttempts] ([ChallengeAttemptId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_AIModels_AIProviderId] ON [AIModels] ([AIProviderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AIProviders_Name] ON [AIProviders] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_AIRequests_AIModelId] ON [AIRequests] ([AIModelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_AIRequests_AIProviderId] ON [AIRequests] ([AIProviderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_AIRequests_DebuggingAttemptId] ON [AIRequests] ([DebuggingAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_AIRequests_UserId] ON [AIRequests] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_ArticleVersions_KnowledgeArticleId] ON [ArticleVersions] ([KnowledgeArticleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_ChallengeAttempts_ChallengeId] ON [ChallengeAttempts] ([ChallengeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_ChallengeAttempts_UserId] ON [ChallengeAttempts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChallengeEvaluations_ChallengeAttemptId] ON [ChallengeEvaluations] ([ChallengeAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_Challenges_KnowledgeArticleId] ON [Challenges] ([KnowledgeArticleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_Challenges_ProblemId] ON [Challenges] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_CodeReviewFindings_CodeReviewId] ON [CodeReviewFindings] ([CodeReviewId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_CodeReviews_GitHubRepositoryId] ON [CodeReviews] ([GitHubRepositoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_CodeReviews_UserId] ON [CodeReviews] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_DebuggingAttempts_ProblemId] ON [DebuggingAttempts] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_DebuggingAttempts_ProblemRequestId] ON [DebuggingAttempts] ([ProblemRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_DeveloperFeedbacks_DebuggingAttemptId] ON [DeveloperFeedbacks] ([DebuggingAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_EmailVerifications_UserId] ON [EmailVerifications] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_GitHubAccounts_UserId] ON [GitHubAccounts] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_GitHubRepositories_GitHubAccountId] ON [GitHubRepositories] ([GitHubAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_KnowledgeArticles_ProblemId] ON [KnowledgeArticles] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_ProblemAttachments_ProblemRequestId] ON [ProblemAttachments] ([ProblemRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_ProblemRequests_ProblemId] ON [ProblemRequests] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Problems_ProblemCode] ON [Problems] ([ProblemCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_Problems_UserId] ON [Problems] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_ProblemSolutions_ProblemId] ON [ProblemSolutions] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_ProblemSolutions_SuccessfulAttemptId] ON [ProblemSolutions] ([SuccessfulAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008160113_FullDB'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008160113_FullDB', N'9.0.2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [Users] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [Users] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [ProblemSolutions] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [ProblemSolutions] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [Problems] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [Problems] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [ProblemAttachments] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [ProblemAttachments] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [KnowledgeArticles] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [KnowledgeArticles] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [GitHubRepositories] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [GitHubRepositories] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [CodeReviews] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [CodeReviews] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [Challenges] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    ALTER TABLE [Challenges] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008194413_AddSoftDeleteEntities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008194413_AddSoftDeleteEntities', N'9.0.2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009131456_AddSecurityAuditLogs'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [AuditLogId] bigint NOT NULL IDENTITY,
        [UserId] int NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(100) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [IpAddress] nvarchar(100) NULL,
        [UserAgent] nvarchar(500) NULL,
        [RequestId] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([AuditLogId]),
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009131456_AddSecurityAuditLogs'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Action] ON [AuditLogs] ([Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009131456_AddSecurityAuditLogs'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [AuditLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009131456_AddSecurityAuditLogs'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_RequestId] ON [AuditLogs] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009131456_AddSecurityAuditLogs'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009131456_AddSecurityAuditLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009131456_AddSecurityAuditLogs', N'9.0.2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE TABLE [AIUsageEvents] (
        [AIUsageEventId] bigint NOT NULL IDENTITY,
        [AIRequestId] int NOT NULL,
        [UserId] int NOT NULL,
        [ProblemId] int NULL,
        [ProviderId] int NOT NULL,
        [ModelId] int NULL,
        [OperationType] nvarchar(100) NOT NULL,
        [InputTokens] int NULL,
        [OutputTokens] int NULL,
        [TotalTokens] int NULL,
        [EstimatedCost] decimal(18,6) NULL,
        [ActualCost] decimal(18,6) NULL,
        [CostType] nvarchar(50) NOT NULL,
        [CreditsReserved] int NULL,
        [CreditsCharged] int NULL,
        [CreditsRefunded] int NULL,
        [Status] nvarchar(50) NOT NULL,
        [ProviderRequestId] nvarchar(150) NULL,
        [CorrelationId] nvarchar(150) NULL,
        [IdempotencyKey] nvarchar(150) NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [DurationMs] bigint NULL,
        [RetrySequence] int NOT NULL,
        [ErrorCode] nvarchar(100) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AIUsageEvents] PRIMARY KEY ([AIUsageEventId]),
        CONSTRAINT [FK_AIUsageEvents_AIModels_ModelId] FOREIGN KEY ([ModelId]) REFERENCES [AIModels] ([AIModelId]),
        CONSTRAINT [FK_AIUsageEvents_AIProviders_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [AIProviders] ([AIProviderId]),
        CONSTRAINT [FK_AIUsageEvents_AIRequests_AIRequestId] FOREIGN KEY ([AIRequestId]) REFERENCES [AIRequests] ([AIRequestId]),
        CONSTRAINT [FK_AIUsageEvents_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([ProblemId]),
        CONSTRAINT [FK_AIUsageEvents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE TABLE [Features] (
        [FeatureId] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Category] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Features] PRIMARY KEY ([FeatureId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE TABLE [Plans] (
        [PlanId] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Status] nvarchar(50) NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsPublic] bit NOT NULL,
        [PeriodType] nvarchar(50) NOT NULL,
        [CreditsPerPeriod] int NOT NULL,
        [Price] decimal(18,2) NULL,
        [Currency] nvarchar(10) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Plans] PRIMARY KEY ([PlanId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE TABLE [PlanFeatures] (
        [PlanId] int NOT NULL,
        [FeatureId] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [LimitValue] int NULL,
        [ConfigJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlanFeatures] PRIMARY KEY ([PlanId], [FeatureId]),
        CONSTRAINT [FK_PlanFeatures_Features_FeatureId] FOREIGN KEY ([FeatureId]) REFERENCES [Features] ([FeatureId]),
        CONSTRAINT [FK_PlanFeatures_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([PlanId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE TABLE [UsagePeriods] (
        [UsagePeriodId] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PlanId] int NOT NULL,
        [PeriodStart] datetime2 NOT NULL,
        [PeriodEnd] datetime2 NOT NULL,
        [CreditsAllocated] int NOT NULL,
        [CreditsReserved] int NOT NULL,
        [CreditsUsed] int NOT NULL,
        [CreditsRefunded] int NOT NULL,
        [RequestCount] bigint NOT NULL,
        [TokenCount] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UsagePeriods] PRIMARY KEY ([UsagePeriodId]),
        CONSTRAINT [FK_UsagePeriods_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([PlanId]),
        CONSTRAINT [FK_UsagePeriods_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE TABLE [UserSubscriptions] (
        [SubscriptionId] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PlanId] int NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [AssignedByUserId] int NULL,
        [AssignmentSource] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CancelledAt] datetime2 NULL,
        [CancellationReason] nvarchar(255) NULL,
        CONSTRAINT [PK_UserSubscriptions] PRIMARY KEY ([SubscriptionId]),
        CONSTRAINT [FK_UserSubscriptions_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([PlanId]),
        CONSTRAINT [FK_UserSubscriptions_Users_AssignedByUserId] FOREIGN KEY ([AssignedByUserId]) REFERENCES [Users] ([UserId]),
        CONSTRAINT [FK_UserSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE TABLE [UsageLedgers] (
        [UsageLedgerId] bigint NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [UsagePeriodId] bigint NOT NULL,
        [AIUsageEventId] bigint NULL,
        [EntryType] nvarchar(50) NOT NULL,
        [Amount] int NOT NULL,
        [Reason] nvarchar(255) NOT NULL,
        [ReferenceId] nvarchar(150) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UsageLedgers] PRIMARY KEY ([UsageLedgerId]),
        CONSTRAINT [FK_UsageLedgers_AIUsageEvents_AIUsageEventId] FOREIGN KEY ([AIUsageEventId]) REFERENCES [AIUsageEvents] ([AIUsageEventId]),
        CONSTRAINT [FK_UsageLedgers_UsagePeriods_UsagePeriodId] FOREIGN KEY ([UsagePeriodId]) REFERENCES [UsagePeriods] ([UsagePeriodId]),
        CONSTRAINT [FK_UsageLedgers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'Category', N'CreatedAt', N'Description', N'Key', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Features]'))
        SET IDENTITY_INSERT [Features] ON;
    EXEC(N'INSERT INTO [Features] ([FeatureId], [Category], [CreatedAt], [Description], [Key], [Name], [UpdatedAt])
    VALUES (1, N''AI'', ''2026-01-01T00:00:00.0000000Z'', N''Access to AI debugging assistant.'', N''ai_debugging'', N''AI Debugging'', ''2026-01-01T00:00:00.0000000Z''),
    (2, N''Quota'', ''2026-01-01T00:00:00.0000000Z'', N''Total AI credits available per billing cycle.'', N''monthly_ai_credits'', N''Monthly AI Credits'', ''2026-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'Category', N'CreatedAt', N'Description', N'Key', N'Name', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Features]'))
        SET IDENTITY_INSERT [Features] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PlanId', N'Code', N'CreatedAt', N'CreditsPerPeriod', N'Currency', N'Description', N'IsDefault', N'IsPublic', N'Name', N'PeriodType', N'Price', N'Status', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] ON;
    EXEC(N'INSERT INTO [Plans] ([PlanId], [Code], [CreatedAt], [CreditsPerPeriod], [Currency], [Description], [IsDefault], [IsPublic], [Name], [PeriodType], [Price], [Status], [UpdatedAt])
    VALUES (1, N''FREE'', ''2026-01-01T00:00:00.0000000Z'', 100, N''USD'', N''Default Free tier with standard AI quota.'', CAST(1 AS bit), CAST(1 AS bit), N''Free'', N''Monthly'', 0.0, N''Active'', ''2026-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PlanId', N'Code', N'CreatedAt', N'CreditsPerPeriod', N'Currency', N'Description', N'IsDefault', N'IsPublic', N'Name', N'PeriodType', N'Price', N'Status', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[Plans]'))
        SET IDENTITY_INSERT [Plans] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'PlanId', N'ConfigJson', N'CreatedAt', N'IsEnabled', N'LimitValue', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[PlanFeatures]'))
        SET IDENTITY_INSERT [PlanFeatures] ON;
    EXEC(N'INSERT INTO [PlanFeatures] ([FeatureId], [PlanId], [ConfigJson], [CreatedAt], [IsEnabled], [LimitValue], [UpdatedAt])
    VALUES (1, 1, N''{"enabled": true}'', ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), 100, ''2026-01-01T00:00:00.0000000Z''),
    (2, 1, N''{"credits": 100}'', ''2026-01-01T00:00:00.0000000Z'', CAST(1 AS bit), 100, ''2026-01-01T00:00:00.0000000Z'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'FeatureId', N'PlanId', N'ConfigJson', N'CreatedAt', N'IsEnabled', N'LimitValue', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[PlanFeatures]'))
        SET IDENTITY_INSERT [PlanFeatures] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_AIUsageEvents_AIRequestId] ON [AIUsageEvents] ([AIRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_AIUsageEvents_CreatedAt] ON [AIUsageEvents] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_AIUsageEvents_ModelId] ON [AIUsageEvents] ([ModelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_AIUsageEvents_OperationType] ON [AIUsageEvents] ([OperationType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_AIUsageEvents_ProblemId] ON [AIUsageEvents] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_AIUsageEvents_ProviderId] ON [AIUsageEvents] ([ProviderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_AIUsageEvents_UserId] ON [AIUsageEvents] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Features_Key] ON [Features] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_PlanFeatures_FeatureId] ON [PlanFeatures] ([FeatureId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Plans_Code] ON [Plans] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UsageLedgers_AIUsageEventId] ON [UsageLedgers] ([AIUsageEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UsageLedgers_CreatedAt] ON [UsageLedgers] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UsageLedgers_UsagePeriodId] ON [UsageLedgers] ([UsagePeriodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UsageLedgers_UserId] ON [UsageLedgers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UsagePeriods_PlanId] ON [UsagePeriods] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UsagePeriods_UserId] ON [UsagePeriods] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UsagePeriods_UserId_PeriodStart_PeriodEnd] ON [UsagePeriods] ([UserId], [PeriodStart], [PeriodEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UserSubscriptions_AssignedByUserId] ON [UserSubscriptions] ([AssignedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UserSubscriptions_PlanId] ON [UserSubscriptions] ([PlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UserSubscriptions_Status] ON [UserSubscriptions] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    CREATE INDEX [IX_UserSubscriptions_UserId] ON [UserSubscriptions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261009140718_AddPlanAndUsageEntities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261009140718_AddPlanAndUsageEntities', N'9.0.2');
END;

COMMIT;
GO

