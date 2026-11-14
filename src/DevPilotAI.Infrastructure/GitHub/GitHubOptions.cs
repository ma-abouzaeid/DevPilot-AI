namespace DevPilotAI.Infrastructure.GitHub;

public class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:5281/api/GitHub/callback";
    public string FrontendReturnUrl { get; set; } = "http://localhost:5500/github.html";
    public string Scopes { get; set; } = "read:user repo";
}
