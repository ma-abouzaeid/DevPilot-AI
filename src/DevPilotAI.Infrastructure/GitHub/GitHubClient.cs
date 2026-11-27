using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace DevPilotAI.Infrastructure.GitHub;

public class GitHubClient : IGitHubClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;

    public GitHubClient(HttpClient httpClient, IOptions<GitHubOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.github.com/");
        }
    }

    public string BuildAuthorizeUrl(string state)
    {
        EnsureConfigured();
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = _options.Scopes,
            ["state"] = state,
            ["allow_signup"] = "true",
            // consent prompt on reconnect for repo scope
            ["prompt"] = "consent"
        };

        return "https://github.com/login/oauth/authorize?" + string.Join("&",
            query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<GitHubOAuthToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw AppError.ServiceUnavailable($"GitHub token exchange failed: {(int)response.StatusCode}", "GITHUB_OAUTH_FAILED");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            var description = root.TryGetProperty("error_description", out var desc) ? desc.GetString() : error.GetString();
            throw AppError.InvalidRequest(description ?? "GitHub OAuth failed.", "GITHUB_OAUTH_FAILED");
        }

        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw AppError.ServiceUnavailable("GitHub did not return an access token.", "GITHUB_OAUTH_FAILED");

        return new GitHubOAuthToken(
            AccessToken: accessToken,
            RefreshToken: root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
            TokenType: root.TryGetProperty("token_type", out var type) ? type.GetString() ?? "bearer" : "bearer",
            Scope: root.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
            ExpiresIn: root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds) ? seconds : null
        );
    }

    public async Task<GitHubUserInfo> GetAuthenticatedUserAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var doc = await SendGitHubJsonAsync(HttpMethod.Get, "user", accessToken, cancellationToken);
        var root = doc.RootElement;
        return new GitHubUserInfo(
            Id: root.GetProperty("id").GetRawText(),
            Login: root.GetProperty("login").GetString() ?? string.Empty,
            Name: root.TryGetProperty("name", out var name) ? name.GetString() : null,
            AvatarUrl: root.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null
        );
    }

    public async Task<IReadOnlyList<GitHubRepoInfo>> ListRepositoriesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        // Simple list; affiliation filter was empty for some tokens.
        using var doc = await SendGitHubJsonAsync(
            HttpMethod.Get,
            "user/repos?per_page=100&sort=updated&direction=desc&visibility=all",
            accessToken,
            cancellationToken);

        return ParseRepoArray(doc.RootElement);
    }

    public async Task<IReadOnlyList<GitHubRepoInfo>> ListPublicRepositoriesAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return [];
        }

        using var doc = await SendGitHubJsonAsync(
            HttpMethod.Get,
            $"users/{Uri.EscapeDataString(username.Trim())}/repos?per_page=100&sort=updated&direction=desc&type=owner",
            accessToken: null,
            cancellationToken);

        return ParseRepoArray(doc.RootElement);
    }

    private static List<GitHubRepoInfo> ParseRepoArray(JsonElement root)
    {
        var list = new List<GitHubRepoInfo>();
        if (root.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in root.EnumerateArray())
        {
            list.Add(new GitHubRepoInfo(
                Id: item.GetProperty("id").GetInt64(),
                Name: item.GetProperty("name").GetString() ?? string.Empty,
                FullName: item.GetProperty("full_name").GetString() ?? string.Empty,
                HtmlUrl: item.GetProperty("html_url").GetString() ?? string.Empty,
                DefaultBranch: item.TryGetProperty("default_branch", out var branch) ? branch.GetString() ?? "main" : "main",
                Private: item.TryGetProperty("private", out var isPrivate) && isPrivate.GetBoolean(),
                Description: item.TryGetProperty("description", out var description) ? description.GetString() : null,
                UpdatedAt: item.TryGetProperty("updated_at", out var updated) && updated.ValueKind == JsonValueKind.String
                    ? DateTimeOffset.Parse(updated.GetString()!)
                    : null
            ));
        }

        return list;
    }

    public async Task<IReadOnlyList<GitHubTreeItem>> GetRepositoryTreeAsync(
        string accessToken,
        string owner,
        string repo,
        string branch,
        CancellationToken cancellationToken = default)
    {
        var path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/git/trees/{Uri.EscapeDataString(branch)}?recursive=1";
        using var doc = await SendGitHubJsonAsync(HttpMethod.Get, path, accessToken, cancellationToken);
        var root = doc.RootElement;
        if (!root.TryGetProperty("tree", out var tree))
        {
            return [];
        }

        var items = new List<GitHubTreeItem>();
        foreach (var item in tree.EnumerateArray())
        {
            items.Add(new GitHubTreeItem(
                Path: item.GetProperty("path").GetString() ?? string.Empty,
                Type: item.GetProperty("type").GetString() ?? string.Empty,
                Sha: item.TryGetProperty("sha", out var sha) ? sha.GetString() ?? string.Empty : string.Empty,
                Size: item.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : null
            ));
        }

        return items;
    }

    public async Task<GitHubFileContent> GetFileContentAsync(
        string accessToken,
        string owner,
        string repo,
        string path,
        string? refName = null,
        CancellationToken cancellationToken = default)
    {
        var encodedPath = string.Join("/", path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        var url = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/contents/{encodedPath}";
        if (!string.IsNullOrWhiteSpace(refName))
        {
            url += $"?ref={Uri.EscapeDataString(refName)}";
        }

        using var doc = await SendGitHubJsonAsync(HttpMethod.Get, url, accessToken, cancellationToken);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            throw AppError.InvalidRequest("Path refers to a directory. Choose a file.", "GITHUB_PATH_IS_DIRECTORY");
        }

        var encoding = root.TryGetProperty("encoding", out var enc) ? enc.GetString() ?? "utf-8" : "utf-8";
        string? content = null;
        if (root.TryGetProperty("content", out var contentNode))
        {
            var raw = contentNode.GetString() ?? string.Empty;
            if (string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
            {
                var cleaned = raw.Replace("\n", string.Empty).Replace("\r", string.Empty);
                content = Encoding.UTF8.GetString(Convert.FromBase64String(cleaned));
            }
            else
            {
                content = raw;
            }
        }

        return new GitHubFileContent(
            Path: root.TryGetProperty("path", out var p) ? p.GetString() ?? path : path,
            Name: root.TryGetProperty("name", out var n) ? n.GetString() ?? path : path,
            Content: content,
            Encoding: encoding,
            Size: root.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : content?.Length ?? 0,
            HtmlUrl: root.TryGetProperty("html_url", out var html) ? html.GetString() : null,
            Sha: root.TryGetProperty("sha", out var sha) ? sha.GetString() ?? string.Empty : string.Empty
        );
    }

    private async Task<JsonDocument> SendGitHubJsonAsync(
        HttpMethod method,
        string relativeUrl,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        if (request.Headers.UserAgent.Count == 0)
        {
            request.Headers.UserAgent.ParseAdd("DevPilot-AI");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw AppError.Unauthorized("GitHub access token is invalid or expired. Reconnect GitHub.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw AppError.Forbidden(
                "GitHub denied repository access. Disconnect and reconnect GitHub, then grant the repo permission.",
                "GITHUB_FORBIDDEN");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw AppError.NotFound("GitHub resource was not found or you do not have access.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw AppError.ServiceUnavailable($"GitHub API error {(int)response.StatusCode}: {TrimBody(body)}", "GITHUB_API_FAILED");
        }

        return JsonDocument.Parse(body);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw AppError.ServiceUnavailable("GitHub OAuth is not configured. Set GitHub:ClientId and GitHub:ClientSecret.", "GITHUB_NOT_CONFIGURED");
        }
    }

    private static string TrimBody(string body) =>
        string.IsNullOrWhiteSpace(body) ? string.Empty : (body.Length <= 300 ? body : body[..300]);
}
