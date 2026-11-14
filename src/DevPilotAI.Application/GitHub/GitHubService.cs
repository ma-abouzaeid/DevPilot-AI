using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.GitHub.DTOs;
using DevPilotAI.Application.Plans;
using DevPilotAI.Application.Problems;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DevPilotAI.Application.GitHub;

public class GitHubService : IGitHubService
{
    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".tsx", ".jsx", ".py", ".java", ".go", ".rb", ".php",
        ".cpp", ".c", ".h", ".hpp", ".kt", ".swift", ".rs", ".sql", ".json", ".yml", ".yaml"
    };

    private static readonly string[] IgnoredPathParts =
    [
        "node_modules/", "bin/", "obj/", ".git/", "dist/", "build/", "vendor/", "packages/"
    ];

    private readonly IApplicationDbContext _db;
    private readonly IGitHubClient _gitHub;
    private readonly ITokenProtector _protector;
    private readonly IAIGateway _aiGateway;
    private readonly IProblemService _problemService;
    private readonly IConfiguration _configuration;
    private readonly IPlanUsageService? _planUsageService;
    private readonly ICreditCostService? _creditCostService;
    private readonly IAuditService? _auditService;

    public GitHubService(
        IApplicationDbContext db,
        IGitHubClient gitHub,
        ITokenProtector protector,
        IAIGateway aiGateway,
        IProblemService problemService,
        IConfiguration configuration,
        IPlanUsageService? planUsageService = null,
        IAuditService? auditService = null,
        ICreditCostService? creditCostService = null)
    {
        _db = db;
        _gitHub = gitHub;
        _protector = protector;
        _aiGateway = aiGateway;
        _problemService = problemService;
        _configuration = configuration;
        _planUsageService = planUsageService;
        _auditService = auditService;
        _creditCostService = creditCostService;
    }

    public async Task<GitHubConnectionStatusDto> GetConnectionStatusAsync(int userId, CancellationToken cancellationToken = default)
    {
        var account = await _db.GitHubAccounts
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (account == null)
        {
            return new GitHubConnectionStatusDto(false, null, null, null);
        }

        if (!TryReadAccessToken(account, out _))
        {
            // Token was encrypted with ephemeral keys (app recycle) — clear so the user can reconnect.
            await RemoveGitHubAccountAsync(account, cancellationToken);
            return new GitHubConnectionStatusDto(false, null, null, null);
        }

        return new GitHubConnectionStatusDto(true, account.GitHubUsername, null, account.ConnectedAt);
    }

    public Task<GitHubAuthorizeUrlDto> GetAuthorizeUrlAsync(int userId, CancellationToken cancellationToken = default)
    {
        var statePayload = $"{userId}|{Guid.NewGuid():N}|{DateTime.UtcNow.Ticks}";
        var state = ToUrlSafe(_protector.Protect(statePayload));
        var url = _gitHub.BuildAuthorizeUrl(state);
        return Task.FromResult(new GitHubAuthorizeUrlDto(url));
    }

    public async Task<string> HandleOAuthCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            throw AppError.InvalidRequest("Missing OAuth code or state.", "GITHUB_OAUTH_INVALID");
        }

        string statePayload;
        try
        {
            statePayload = _protector.Unprotect(FromUrlSafe(state));
        }
        catch
        {
            throw AppError.InvalidRequest("Invalid or expired OAuth state.", "GITHUB_OAUTH_STATE_INVALID");
        }

        var parts = statePayload.Split('|');
        if (parts.Length < 1 || !int.TryParse(parts[0], out var userId) || userId <= 0)
        {
            throw AppError.InvalidRequest("OAuth state does not contain a valid user.", "GITHUB_OAUTH_STATE_INVALID");
        }

        var token = await _gitHub.ExchangeCodeAsync(code, cancellationToken);
        var ghUser = await _gitHub.GetAuthenticatedUserAsync(token.AccessToken, cancellationToken);
        var encrypted = _protector.Protect(SerializeTokenBlob(token));

        var account = await _db.GitHubAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account == null)
        {
            account = new GitHubAccount
            {
                UserId = userId,
                GitHubUserId = ghUser.Id,
                GitHubUsername = ghUser.Login,
                AccessTokenEncrypted = encrypted,
                ConnectedAt = DateTime.UtcNow
            };
            _db.GitHubAccounts.Add(account);
        }
        else
        {
            account.GitHubUserId = ghUser.Id;
            account.GitHubUsername = ghUser.Login;
            account.AccessTokenEncrypted = encrypted;
            account.ConnectedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return BuildFrontendReturnUrl(new Dictionary<string, string?>
        {
            ["connected"] = "1",
            ["user"] = ghUser.Login
        });
    }

    public string BuildFrontendReturnUrl(IReadOnlyDictionary<string, string?> query)
    {
        var returnUrl = _configuration["GitHub:FrontendReturnUrl"];
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            var baseUrl = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:5500").TrimEnd('/');
            returnUrl = $"{baseUrl}/github.html";
        }

        var parts = query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
            .ToList();

        if (parts.Count == 0)
        {
            return returnUrl;
        }

        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{returnUrl}{separator}{string.Join("&", parts)}";
    }

    public async Task DisconnectAsync(int userId, CancellationToken cancellationToken = default)
    {
        var account = await _db.GitHubAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (account == null)
        {
            // Idempotent: already disconnected (or broken row already cleaned up).
            return;
        }

        await RemoveGitHubAccountAsync(account, cancellationToken);
    }

    public async Task<IReadOnlyList<GitHubRepoDto>> ListRepositoriesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var (account, accessToken) = await GetConnectedAccountAsync(userId, cancellationToken);
        IReadOnlyList<GitHubRepoInfo> repos;
        try
        {
            repos = await _gitHub.ListRepositoriesAsync(accessToken, cancellationToken);
        }
        catch (Exception)
        {
            // Fall back to public repos for the connected username so the UI is not empty
            // when the OAuth token is missing/invalid for private listing.
            repos = await _gitHub.ListPublicRepositoriesAsync(account.GitHubUsername, cancellationToken);
            if (repos.Count == 0)
            {
                throw;
            }
        }

        if (repos.Count == 0 && !string.IsNullOrWhiteSpace(account.GitHubUsername))
        {
            repos = await _gitHub.ListPublicRepositoriesAsync(account.GitHubUsername, cancellationToken);
        }

        try
        {
            foreach (var repo in repos.Take(50))
            {
                await UpsertRepositoryAsync(account, repo, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Listing must not fail just because local cache upsert failed.
        }

        return repos.Select(r => new GitHubRepoDto(
            r.Name,
            r.FullName,
            r.HtmlUrl,
            r.DefaultBranch,
            r.Private,
            r.Description
        )).ToList();
    }

    public async Task<IReadOnlyList<GitHubTreeItemDto>> GetRepositoryTreeAsync(
        int userId,
        string fullName,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        var (_, accessToken) = await GetConnectedAccountAsync(userId, cancellationToken);
        var (owner, repo) = SplitFullName(fullName);
        var resolvedBranch = string.IsNullOrWhiteSpace(branch) ? "main" : branch.Trim();

        IReadOnlyList<GitHubTreeItem> tree;
        try
        {
            tree = await _gitHub.GetRepositoryTreeAsync(accessToken, owner, repo, resolvedBranch, cancellationToken);
        }
        catch when (resolvedBranch == "main")
        {
            tree = await _gitHub.GetRepositoryTreeAsync(accessToken, owner, repo, "master", cancellationToken);
        }

        return tree
            .Where(t => t.Type == "blob")
            .Where(t => !IgnoredPathParts.Any(p => t.Path.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .Select(t => new GitHubTreeItemDto(t.Path, t.Type, t.Size))
            .OrderBy(t => t.Path)
            .Take(500)
            .ToList();
    }

    public async Task<GitHubFileDto> GetFileAsync(
        int userId,
        string fullName,
        string path,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        var (_, accessToken) = await GetConnectedAccountAsync(userId, cancellationToken);
        var (owner, repo) = SplitFullName(fullName);
        var file = await _gitHub.GetFileContentAsync(accessToken, owner, repo, path, branch, cancellationToken);
        if (string.IsNullOrWhiteSpace(file.Content))
        {
            throw AppError.InvalidRequest("File content is empty or binary and cannot be used.", "GITHUB_EMPTY_FILE");
        }

        if (file.Content.Length > 50000)
        {
            throw AppError.InvalidRequest("File is too large (max 50,000 characters).", "GITHUB_FILE_TOO_LARGE");
        }

        return new GitHubFileDto(file.Path, file.Name, file.Content, file.Size, file.HtmlUrl);
    }

    public async Task<GitHubCodeReviewResultDto> ReviewAsync(
        int userId,
        GitHubCodeReviewRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (_planUsageService != null)
        {
            await _planUsageService.EnsureFeatureAsync(userId, PlanFeatureKeys.GitHubCodeReview, cancellationToken);
        }

        var (account, accessToken) = await GetConnectedAccountAsync(userId, cancellationToken);
        var (owner, repoName) = SplitFullName(dto.FullName);
        var branch = string.IsNullOrWhiteSpace(dto.Branch) ? "main" : dto.Branch.Trim();

        var paths = (dto.Paths ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(dto.MaxFiles, 1, 12))
            .ToList();

        if (paths.Count == 0)
        {
            IReadOnlyList<GitHubTreeItem> tree;
            try
            {
                tree = await _gitHub.GetRepositoryTreeAsync(accessToken, owner, repoName, branch, cancellationToken);
            }
            catch
            {
                tree = await _gitHub.GetRepositoryTreeAsync(accessToken, owner, repoName, "master", cancellationToken);
                branch = "master";
            }

            paths = tree
                .Where(t => t.Type == "blob")
                .Where(t => !IgnoredPathParts.Any(p => t.Path.Contains(p, StringComparison.OrdinalIgnoreCase)))
                .Where(t => SourceExtensions.Contains(Path.GetExtension(t.Path)))
                .Where(t => t.Size is null or <= 80000)
                .OrderByDescending(t => t.Size ?? 0)
                .Take(Math.Clamp(dto.MaxFiles, 1, 12))
                .Select(t => t.Path)
                .ToList();
        }

        if (paths.Count == 0)
        {
            throw AppError.InvalidRequest("No reviewable source files were found in this repository.", "GITHUB_NO_FILES");
        }

        var bundles = new List<(string Path, string Content)>();
        var totalChars = 0;
        foreach (var path in paths)
        {
            var file = await _gitHub.GetFileContentAsync(accessToken, owner, repoName, path, branch, cancellationToken);
            if (string.IsNullOrWhiteSpace(file.Content)) continue;
            if (file.Content.Length > 20000)
            {
                file = file with { Content = file.Content[..20000] + "\n// ... truncated ..." };
            }

            if (totalChars + file.Content.Length > 70000) break;
            bundles.Add((path, file.Content));
            totalChars += file.Content.Length;
        }

        if (bundles.Count == 0)
        {
            throw AppError.InvalidRequest("Could not load any file contents for review.", "GITHUB_NO_CONTENT");
        }

        var systemPrompt = """
            You are DevPilot AI performing a professional code review.
            Return ONE JSON object only (no markdown fences) with this schema:
            {
              "summary": "short overall review summary",
              "findings": [
                {
                  "filePath": "path/to/file",
                  "lineNumber": 12,
                  "severity": "Low|Medium|High|Critical",
                  "category": "bug|security|performance|style|maintainability",
                  "description": "what is wrong",
                  "suggestedFix": "how to fix it"
                }
              ]
            }
            Rules:
            - Always include summary and findings array (can be empty).
            - Prefer concrete, actionable findings.
            - lineNumber may be null when unknown.
            """;

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Repository: {dto.FullName}");
        userPrompt.AppendLine($"Branch: {branch}");
        userPrompt.AppendLine("Review the following files:");
        foreach (var (path, content) in bundles)
        {
            userPrompt.AppendLine();
            userPrompt.AppendLine($"--- FILE: {path} ---");
            userPrompt.AppendLine(content);
        }

        var reviewCost = _creditCostService != null
            ? await _creditCostService.GetCostAsync("CodeReview", cancellationToken)
            : CreditCosts.CodeReview;

        if (_planUsageService != null)
        {
            await _planUsageService.EnsureCreditsAsync(userId, reviewCost, cancellationToken);
        }

        var ai = await _aiGateway.ExecuteAsync(new AIGatewayRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt.ToString(),
            TaskType: "CodeReview",
            UserId: userId,
            Purpose: $"GitHub code review for {dto.FullName} ({branch})",
            MaxTokens: 4096,
            Temperature: 0.2
        ), cancellationToken);

        if (!ai.IsSuccess)
        {
            throw AppError.ServiceUnavailable(ai.ErrorMessage ?? "AI code review failed.", "AI_GATEWAY_UNAVAILABLE");
        }

        if (_planUsageService != null)
        {
            await _planUsageService.ChargeCreditsAsync(
                userId,
                reviewCost,
                $"GitHub code review for {dto.FullName}",
                referenceId: $"github-review:{dto.FullName}",
                aiUsageEventId: ai.AIUsageEventId,
                metadataJson: JsonSerializer.Serialize(new
                {
                    taskType = "CodeReview",
                    repo = dto.FullName,
                    branch,
                    files = bundles.Count,
                    aiRequestId = ai.AIRequestId,
                    provider = ai.ProviderName
                }),
                tokensUsed: ai.TotalTokens,
                cancellationToken: cancellationToken);
        }

        var parsed = ParseReviewPayload(ai.Content);
        var repoEntity = await UpsertRepositoryAsync(account, new GitHubRepoInfo(0, repoName, dto.FullName, $"https://github.com/{dto.FullName}", branch, false, null, null), cancellationToken);

        var review = new CodeReview
        {
            UserId = userId,
            GitHubRepositoryId = repoEntity.GitHubRepositoryId,
            BranchName = branch,
            TargetTargetRef = string.Join(", ", bundles.Select(b => b.Path)),
            ReviewSummary = parsed.Summary,
            ReviewedAt = DateTime.UtcNow
        };

        foreach (var finding in parsed.Findings)
        {
            review.Findings.Add(new CodeReviewFinding
            {
                FilePath = finding.FilePath,
                LineNumber = finding.LineNumber,
                Severity = ParseSeverity(finding.Severity),
                Category = finding.Category,
                Description = finding.Description,
                SuggestedFix = finding.SuggestedFix
            });
        }

        _db.CodeReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SystemAuditEventTypes.GithubReviewCompleted,
                nameof(CodeReview),
                review.CodeReviewId.ToString(),
                oldValues: null,
                newValues: new
                {
                    review.CodeReviewId,
                    dto.FullName,
                    branch,
                    Findings = review.Findings.Count,
                    ai.AIRequestId,
                    ai.AIUsageEventId,
                    ai.ProviderName,
                    Credits = reviewCost
                },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return new GitHubCodeReviewResultDto(
            review.CodeReviewId,
            dto.FullName,
            branch,
            parsed.Summary,
            bundles.Select(b => b.Path).ToList(),
            parsed.Findings.Select(f => new GitHubCodeReviewFindingDto(
                f.FilePath,
                f.LineNumber,
                f.Severity,
                f.Category,
                f.Description,
                f.SuggestedFix
            )).ToList(),
            ai.ProviderName
        );
    }

    public async Task<DebuggingResultDto> DebugFromFileAsync(
        int userId,
        GitHubDebugRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var file = await GetFileAsync(userId, dto.FullName, dto.Path, dto.Branch, cancellationToken);
        var description = string.IsNullOrWhiteSpace(dto.Description)
            ? $"Debugging GitHub file {dto.FullName}:{dto.Path}"
            : dto.Description.Trim();

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SystemAuditEventTypes.GithubDebugStarted,
                "GitHubFile",
                $"{dto.FullName}:{dto.Path}",
                oldValues: null,
                newValues: new { dto.FullName, dto.Path, dto.Branch },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return await _problemService.SubmitProblemAsync(new CreateProblemDto
        {
            UserId = userId,
            Title = null,
            Description = description,
            ErrorMessage = dto.ErrorMessage.Trim(),
            SourceCode = file.Content,
            Framework = GuessFramework(dto.Path)
        }, cancellationToken);
    }

    private async Task<(GitHubAccount Account, string AccessToken)> GetConnectedAccountAsync(int userId, CancellationToken cancellationToken)
    {
        var account = await _db.GitHubAccounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken)
            ?? throw AppError.InvalidRequest("Connect GitHub first.", "GITHUB_NOT_CONNECTED");

        if (!TryReadAccessToken(account, out var accessToken) || string.IsNullOrWhiteSpace(accessToken))
        {
            await RemoveGitHubAccountAsync(account, cancellationToken);
            throw AppError.InvalidRequest(
                "GitHub connection expired after a server restart. Click Connect GitHub again.",
                "GITHUB_NOT_CONNECTED");
        }

        return (account, accessToken!);
    }

    private bool TryReadAccessToken(GitHubAccount account, out string? accessToken)
    {
        accessToken = null;
        try
        {
            var blob = _protector.Unprotect(account.AccessTokenEncrypted);
            accessToken = DeserializeAccessToken(blob);
            return !string.IsNullOrWhiteSpace(accessToken);
        }
        catch
        {
            return false;
        }
    }

    private async Task RemoveGitHubAccountAsync(GitHubAccount account, CancellationToken cancellationToken)
    {
        // Hard-delete repos (soft-delete leaves FK rows that block account removal with DeleteBehavior.NoAction).
        var repos = await _db.GitHubRepositories
            .IgnoreQueryFilters()
            .Where(r => r.GitHubAccountId == account.GitHubAccountId)
            .ToListAsync(cancellationToken);

        if (repos.Count > 0)
        {
            var repoIds = repos.Select(r => r.GitHubRepositoryId).ToList();
            var reviews = await _db.CodeReviews
                .IgnoreQueryFilters()
                .Where(r => r.GitHubRepositoryId != null && repoIds.Contains(r.GitHubRepositoryId.Value))
                .ToListAsync(cancellationToken);
            foreach (var review in reviews)
            {
                review.GitHubRepositoryId = null;
            }

            _db.GitHubRepositories.RemoveRange(repos);
        }

        _db.GitHubAccounts.Remove(account);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<GitHubRepository> UpsertRepositoryAsync(GitHubAccount account, GitHubRepoInfo repo, CancellationToken cancellationToken)
    {
        var existing = await _db.GitHubRepositories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.GitHubAccountId == account.GitHubAccountId && r.FullName == repo.FullName, cancellationToken);

        if (existing == null)
        {
            existing = new GitHubRepository
            {
                GitHubAccountId = account.GitHubAccountId,
                RepoName = repo.Name,
                FullName = repo.FullName,
                RepoUrl = repo.HtmlUrl,
                DefaultBranch = repo.DefaultBranch,
                IsPrivate = repo.Private
            };
            _db.GitHubRepositories.Add(existing);
        }
        else
        {
            existing.RepoName = repo.Name;
            existing.RepoUrl = repo.HtmlUrl;
            existing.DefaultBranch = repo.DefaultBranch;
            existing.IsPrivate = repo.Private;
            existing.IsDeleted = false;
            existing.DeletedAt = null;
        }

        return existing;
    }

    private static (string Owner, string Repo) SplitFullName(string fullName)
    {
        var parts = (fullName ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw AppError.InvalidRequest("Repository full name must be owner/repo.", "GITHUB_INVALID_REPO");
        }

        return (parts[0], parts[1]);
    }

    private static string SerializeTokenBlob(GitHubOAuthToken token) =>
        JsonSerializer.Serialize(new
        {
            access_token = token.AccessToken,
            refresh_token = token.RefreshToken,
            token_type = token.TokenType,
            scope = token.Scope,
            expires_in = token.ExpiresIn,
            stored_at = DateTime.UtcNow
        });

    private static string DeserializeAccessToken(string blob)
    {
        using var doc = JsonDocument.Parse(blob);
        if (doc.RootElement.TryGetProperty("access_token", out var token))
        {
            return token.GetString()
                ?? throw AppError.Unauthorized("Stored GitHub token is empty.");
        }

        // Backward compatible: raw token string
        return blob;
    }

    private static string ToUrlSafe(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string FromUrlSafe(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private static ReviewSeverity ParseSeverity(string? severity) =>
        severity?.Trim().ToLowerInvariant() switch
        {
            "low" => ReviewSeverity.Low,
            "high" => ReviewSeverity.High,
            "critical" => ReviewSeverity.Critical,
            _ => ReviewSeverity.Medium
        };

    private static string? GuessFramework(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "C# / .NET",
            ".ts" or ".tsx" => "TypeScript",
            ".js" or ".jsx" => "JavaScript",
            ".py" => "Python",
            ".java" => "Java",
            ".go" => "Go",
            _ => null
        };

    private static ReviewPayload ParseReviewPayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ReviewPayload("AI returned an empty review.", []);
        }

        var text = raw.Trim();
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            text = text[start..(end + 1)];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ReviewPayloadJson>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed != null)
            {
                return new ReviewPayload(
                    string.IsNullOrWhiteSpace(parsed.Summary) ? "Code review completed." : parsed.Summary.Trim(),
                    (parsed.Findings ?? []).Select(f => new ReviewFindingPayload(
                        string.IsNullOrWhiteSpace(f.FilePath) ? "unknown" : f.FilePath.Trim(),
                        f.LineNumber,
                        string.IsNullOrWhiteSpace(f.Severity) ? "Medium" : f.Severity.Trim(),
                        string.IsNullOrWhiteSpace(f.Category) ? "general" : f.Category.Trim(),
                        f.Description?.Trim() ?? string.Empty,
                        f.SuggestedFix?.Trim() ?? string.Empty
                    )).ToList());
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return new ReviewPayload("Structured parse fallback was used.",
        [
            new ReviewFindingPayload("unknown", null, "Medium", "general", text.Length > 2000 ? text[..2000] : text, string.Empty)
        ]);
    }

    private sealed record ReviewPayload(string Summary, List<ReviewFindingPayload> Findings);
    private sealed record ReviewFindingPayload(string FilePath, int? LineNumber, string Severity, string Category, string Description, string SuggestedFix);

    private sealed class ReviewPayloadJson
    {
        public string? Summary { get; set; }
        public List<ReviewFindingJson>? Findings { get; set; }
    }

    private sealed class ReviewFindingJson
    {
        public string? FilePath { get; set; }
        public int? LineNumber { get; set; }
        public string? Severity { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? SuggestedFix { get; set; }
    }
}
