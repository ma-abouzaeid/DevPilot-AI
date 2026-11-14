using DevPilotAI.Application.GitHub.DTOs;
using DevPilotAI.Application.Problems.DTOs;

namespace DevPilotAI.Application.GitHub;

public interface IGitHubService
{
    Task<GitHubConnectionStatusDto> GetConnectionStatusAsync(int userId, CancellationToken cancellationToken = default);
    Task<GitHubAuthorizeUrlDto> GetAuthorizeUrlAsync(int userId, CancellationToken cancellationToken = default);
    Task<string> HandleOAuthCallbackAsync(string code, string state, CancellationToken cancellationToken = default);
    Task DisconnectAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubRepoDto>> ListRepositoriesAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubTreeItemDto>> GetRepositoryTreeAsync(int userId, string fullName, string? branch = null, CancellationToken cancellationToken = default);
    Task<GitHubFileDto> GetFileAsync(int userId, string fullName, string path, string? branch = null, CancellationToken cancellationToken = default);
    Task<GitHubCodeReviewResultDto> ReviewAsync(int userId, GitHubCodeReviewRequestDto dto, CancellationToken cancellationToken = default);
    Task<DebuggingResultDto> DebugFromFileAsync(int userId, GitHubDebugRequestDto dto, CancellationToken cancellationToken = default);
}
