using DevPilotAI.Domain.Common;
using DevPilotAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<EmailVerification> EmailVerifications { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Problem> Problems { get; }
    DbSet<ProblemRequest> ProblemRequests { get; }
    DbSet<ProblemAttachment> ProblemAttachments { get; }
    DbSet<DebuggingAttempt> DebuggingAttempts { get; }
    DbSet<DeveloperFeedback> DeveloperFeedbacks { get; }
    DbSet<ProblemSolution> ProblemSolutions { get; }

    DbSet<KnowledgeArticle> KnowledgeArticles { get; }
    DbSet<ArticleVersion> ArticleVersions { get; }

    DbSet<Challenge> Challenges { get; }
    DbSet<ChallengeAttempt> ChallengeAttempts { get; }
    DbSet<ChallengeEvaluation> ChallengeEvaluations { get; }

    DbSet<GitHubAccount> GitHubAccounts { get; }
    DbSet<GitHubRepository> GitHubRepositories { get; }
    DbSet<CodeReview> CodeReviews { get; }
    DbSet<CodeReviewFinding> CodeReviewFindings { get; }

    DbSet<AIProvider> AIProviders { get; }
    DbSet<AIModel> AIModels { get; }
    DbSet<AIRequest> AIRequests { get; }

    void SoftDelete<TEntity>(TEntity entity) where TEntity : class, ISoftDeletable;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
