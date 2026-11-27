using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.GitHub.DTOs;

public record GitHubConnectionStatusDto(
    bool IsConnected,
    string? Username,
    string? AvatarUrl,
    DateTime? ConnectedAt
);

public record GitHubAuthorizeUrlDto(string AuthorizeUrl);

public record GitHubRepoDto(
    string Name,
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    bool IsPrivate,
    string? Description
);

public record GitHubTreeItemDto(
    string Path,
    string Type,
    long? Size
);

public record GitHubFileDto(
    string Path,
    string Name,
    string Content,
    long Size,
    string? HtmlUrl
);

public class GitHubCodeReviewRequestDto
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Branch { get; set; }

    // Empty = pick files automatically.
    public IReadOnlyList<string>? Paths { get; set; }

    [Range(1, 12)]
    public int MaxFiles { get; set; } = 6;
}

public record GitHubCodeReviewFindingDto(
    string FilePath,
    int? LineNumber,
    string Severity,
    string Category,
    string Description,
    string SuggestedFix
);

public record GitHubCodeReviewResultDto(
    int CodeReviewId,
    string RepositoryFullName,
    string Branch,
    string Summary,
    IReadOnlyList<string> ReviewedPaths,
    IReadOnlyList<GitHubCodeReviewFindingDto> Findings,
    string ProviderUsed,
    DateTime? ReviewedAt = null
);

public record GitHubCodeReviewListItemDto(
    int CodeReviewId,
    string RepositoryFullName,
    string Branch,
    string SummaryPreview,
    int FindingsCount,
    DateTime ReviewedAt,
    string? TargetFiles
);

public class GitHubDebugRequestDto
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Path { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [StringLength(4000)]
    public string ErrorMessage { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Branch { get; set; }

    [StringLength(10000)]
    public string? Description { get; set; }
}
