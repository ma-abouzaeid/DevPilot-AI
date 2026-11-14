namespace DevPilotAI.Application.Common.Interfaces;

public record GitHubOAuthToken(
    string AccessToken,
    string? RefreshToken,
    string TokenType,
    string? Scope,
    int? ExpiresIn
);

public record GitHubUserInfo(
    string Id,
    string Login,
    string? Name,
    string? AvatarUrl
);

public record GitHubRepoInfo(
    long Id,
    string Name,
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    bool Private,
    string? Description,
    DateTimeOffset? UpdatedAt
);

public record GitHubTreeItem(
    string Path,
    string Type,
    string Sha,
    long? Size
);

public record GitHubFileContent(
    string Path,
    string Name,
    string? Content,
    string Encoding,
    long Size,
    string? HtmlUrl,
    string Sha
);

public interface IGitHubClient
{
    string BuildAuthorizeUrl(string state);
    Task<GitHubOAuthToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<GitHubUserInfo> GetAuthenticatedUserAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubRepoInfo>> ListRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubTreeItem>> GetRepositoryTreeAsync(string accessToken, string owner, string repo, string branch, CancellationToken cancellationToken = default);
    Task<GitHubFileContent> GetFileContentAsync(string accessToken, string owner, string repo, string path, string? refName = null, CancellationToken cancellationToken = default);
}
