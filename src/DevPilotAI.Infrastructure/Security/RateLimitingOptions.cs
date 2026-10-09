namespace DevPilotAI.Infrastructure.Security;

public static class RateLimitPolicies
{
    public const string General = "General";
    public const string AiAnalysis = "AiAnalysis";
    public const string AiFollowUp = "AiFollowUp";
    public const string ArticleGeneration = "ArticleGeneration";
    public const string Login = "Login";
    public const string Register = "Register";
    public const string AiConcurrency = "AiConcurrency";
}

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public PolicyOptions General { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };
    public PolicyOptions AiAnalysis { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };
    public PolicyOptions AiFollowUp { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public PolicyOptions ArticleGeneration { get; set; } = new() { PermitLimit = 3, WindowSeconds = 60 };
    public PolicyOptions Login { get; set; } = new() { PermitLimit = 5, WindowSeconds = 300 };
    public PolicyOptions Register { get; set; } = new() { PermitLimit = 5, WindowSeconds = 300 };
    public ConcurrencyPolicyOptions AiConcurrency { get; set; } = new() { PermitLimit = 1, QueueLimit = 0 };
}

public class PolicyOptions
{
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int SegmentsPerWindow { get; set; } = 1;
}

public class ConcurrencyPolicyOptions
{
    public int PermitLimit { get; set; } = 1;
    public int QueueLimit { get; set; } = 0;
}
