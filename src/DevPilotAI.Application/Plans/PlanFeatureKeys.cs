namespace DevPilotAI.Application.Plans;

public static class PlanFeatureKeys
{
    public const string AiDebugging = "ai_debugging";
    public const string MonthlyAiCredits = "monthly_ai_credits";
    public const string GitHubCodeReview = "github_code_review";
}

public static class PlanCodes
{
    public const string Free = "FREE";
    public const string Pro = "PRO";
}

/// <summary>Credit costs shown on plans UI (tokens are internal).</summary>
public static class CreditCosts
{
    public const int Debugging = 5;
    public const int KnowledgeAdaptation = 2;
    public const int ChallengeGeneration = 3;
    public const int ChallengeEvaluation = 5;
    public const int CodeReview = 15;

    public static int ForTaskType(string? taskType) =>
        (taskType ?? string.Empty).Trim() switch
        {
            "Debugging" => Debugging,
            "KnowledgeAdaptation" => KnowledgeAdaptation,
            "ChallengeGeneration" => ChallengeGeneration,
            "ChallengeEvaluation" => ChallengeEvaluation,
            "CodeReview" => CodeReview,
            _ => Debugging
        };
}
