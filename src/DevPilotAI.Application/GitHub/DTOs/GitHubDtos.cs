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

public record GitHubCodeReviewRequestDto(
    [Required]
    [StringLength(200)]
    [property: Required]
    [property: StringLength(200)]
    string FullName,

    [StringLength(200)]
    [property: StringLength(200)]
    string? Branch = null,

    /// <summary>Specific files to review. Empty = auto-select top source files.</summary>
    IReadOnlyList<string>? Paths = null,

    [Range(1, 12)]
    [property: Range(1, 12)]
    int MaxFiles = 6
);

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
    string ProviderUsed
);

public record GitHubDebugRequestDto(
    [Required]
    [StringLength(200)]
    [property: Required]
    [property: StringLength(200)]
    string FullName,

    [Required]
    [StringLength(500)]
    [property: Required]
    [property: StringLength(500)]
    string Path,

    [Required]
    [MinLength(2)]
    [StringLength(4000)]
    [property: Required]
    [property: MinLength(2)]
    [property: StringLength(4000)]
    string ErrorMessage,

    [StringLength(200)]
    [property: StringLength(200)]
    string? Branch = null,

    [StringLength(10000)]
    [property: StringLength(10000)]
    string? Description = null
);
