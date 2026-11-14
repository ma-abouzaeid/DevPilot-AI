using System.Text.Json;
using System.Text.RegularExpressions;
using DevPilotAI.Application.Challenges.DTOs;
using DevPilotAI.Application.Common.Exceptions;
using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Plans;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevPilotAI.Application.Challenges;

public class ChallengeService : IChallengeService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex RequirementsSplitRegex = new(@"(?=\d+\.\s+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RequirementsItemRegex = new(@"^\d+\.\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IApplicationDbContext _db;
    private readonly IAIGateway _aiGateway;
    private readonly IPlanUsageService? _planUsageService;
    private readonly ICreditCostService? _creditCostService;
    private readonly IAuditService? _auditService;

    public ChallengeService(
        IApplicationDbContext db,
        IAIGateway aiGateway,
        IPlanUsageService? planUsageService = null,
        IAuditService? auditService = null,
        ICreditCostService? creditCostService = null)
    {
        _db = db;
        _aiGateway = aiGateway;
        _planUsageService = planUsageService;
        _auditService = auditService;
        _creditCostService = creditCostService;
    }

    public async Task<Challenge?> GenerateFromSolvedProblemAsync(
        Problem problem,
        KnowledgeArticle article,
        DebuggingAttempt successfulAttempt,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.Challenges
            .FirstOrDefaultAsync(c => c.ProblemId == problem.ProblemId && !c.IsDeleted, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var framework = await _db.ProblemRequests
            .AsNoTracking()
            .Where(r => r.ProblemId == problem.ProblemId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.Framework)
            .FirstOrDefaultAsync(cancellationToken);

        var systemPrompt = """
            You create short practice coding challenges from a solved bug.
            Return ONE JSON object only (no markdown fences):
            {
              "title": "short challenge title",
              "description": "what the buggy scenario is (2-4 sentences)",
              "requirements": [
                "first concrete requirement",
                "second concrete requirement",
                "third concrete requirement"
              ],
              "starterCode": "a SMALL different code snippet that contains a similar bug (not a copy of the original)",
              "difficulty": "Beginner|Intermediate|Advanced"
            }
            Rules:
            - Keep starterCode small (10-40 lines max).
            - Same error class / root-cause family, but DIFFERENT code.
            - Do not include the fixed solution in starterCode.
            - Prefer practical, compilable-looking snippets.
            - requirements MUST be a JSON array of 3 to 5 short actionable items (not one paragraph).
            """;

        var userPrompt = $"""
            Original problem title: {problem.Title}
            Problem summary: {problem.Summary}
            Framework hint: {framework ?? "unknown"}
            Root cause: {successfulAttempt.RootCauseAnalysis}
            Solution summary: {successfulAttempt.ProposedSolution}
            Article title: {article.Title}

            Create a mini challenge for other developers to practice this fix.
            """;

        var generationCost = _creditCostService != null
            ? await _creditCostService.GetCostAsync("ChallengeGeneration", cancellationToken)
            : CreditCosts.ChallengeGeneration;

        if (_planUsageService != null)
        {
            try
            {
                await _planUsageService.EnsureCreditsAsync(problem.UserId, generationCost, cancellationToken);
            }
            catch
            {
                // Soft-fail: solving a problem should not break because credits ran out.
                return null;
            }
        }

        var ai = await _aiGateway.ExecuteAsync(new AIGatewayRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            TaskType: "ChallengeGeneration",
            UserId: problem.UserId,
            ProblemId: problem.ProblemId,
            Purpose: $"Generate practice challenge from {problem.ProblemCode}",
            MaxTokens: 2048,
            Temperature: 0.35
        ), cancellationToken);

        if (!ai.IsSuccess || string.IsNullOrWhiteSpace(ai.Content))
        {
            // Soft-fail: solving a problem should not break because challenge generation failed.
            return null;
        }

        if (_planUsageService != null)
        {
            try
            {
                await _planUsageService.ChargeCreditsAsync(
                    problem.UserId,
                    generationCost,
                    $"Challenge generation for problem {problem.ProblemCode}",
                    referenceId: $"challenge-gen:{problem.ProblemId}",
                    aiUsageEventId: ai.AIUsageEventId,
                    metadataJson: JsonSerializer.Serialize(new
                    {
                        taskType = "ChallengeGeneration",
                        problem.ProblemId,
                        aiRequestId = ai.AIRequestId,
                        provider = ai.ProviderName
                    }),
                    tokensUsed: ai.TotalTokens,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Soft-fail if charge races to empty; challenge still useful.
            }
        }

        var generated = ParseGeneratedChallenge(ai.Content);
        var difficulty = ParseDifficulty(generated.Difficulty);
        var challenge = new Challenge
        {
            ProblemId = problem.ProblemId,
            KnowledgeArticleId = article.KnowledgeArticleId,
            Title = string.IsNullOrWhiteSpace(generated.Title)
                ? $"Practice: {Truncate(problem.Title, 160) ?? "solved bug"}"
                : Truncate(generated.Title, 200)!,
            Description = string.IsNullOrWhiteSpace(generated.Description)
                ? $"Practice fixing a bug similar to {problem.Title}."
                : generated.Description.Trim(),
            Requirements = FormatRequirementsList(generated.Requirements),
            StarterCode = string.IsNullOrWhiteSpace(generated.StarterCode)
                ? "// TODO: fix the bug\n"
                : generated.StarterCode.Trim(),
            Difficulty = difficulty,
            MaxPoints = MaxPointsFor(difficulty),
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Challenges.Add(challenge);
        await _db.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SystemAuditEventTypes.ChallengeGenerated,
                nameof(Challenge),
                challenge.ChallengeId.ToString(),
                oldValues: null,
                newValues: new
                {
                    challenge.ChallengeId,
                    challenge.Title,
                    problem.ProblemId,
                    ai.AIRequestId,
                    ai.AIUsageEventId,
                    ai.ProviderName
                },
                userId: problem.UserId,
                cancellationToken: cancellationToken);
        }

        return challenge;
    }

    public async Task<IReadOnlyList<ChallengeListItemDto>> ListPublishedAsync(int? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var challenges = await _db.Challenges.AsNoTracking()
            .Where(c => c.IsPublished && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.ChallengeId,
                c.Title,
                c.Description,
                Difficulty = c.Difficulty.ToString(),
                c.MaxPoints,
                c.CreatedAt,
                ProblemCode = c.Problem.ProblemCode
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        Dictionary<int, (bool Solved, int Best)> userMap = new();
        if (currentUserId.HasValue)
        {
            var stats = await _db.ChallengeAttempts.AsNoTracking()
                .Where(a => a.UserId == currentUserId.Value && a.Evaluation != null)
                .GroupBy(a => a.ChallengeId)
                .Select(g => new
                {
                    ChallengeId = g.Key,
                    Solved = g.Any(x => x.Evaluation!.IsPassed),
                    Best = g.Max(x => x.Evaluation!.PointsAwarded)
                })
                .ToListAsync(cancellationToken);

            userMap = stats.ToDictionary(x => x.ChallengeId, x => (x.Solved, x.Best));
        }

        return challenges.Select(c =>
        {
            userMap.TryGetValue(c.ChallengeId, out var u);
            return new ChallengeListItemDto(
                c.ChallengeId,
                c.Title,
                c.Description,
                c.Difficulty,
                c.MaxPoints,
                c.CreatedAt,
                c.ProblemCode,
                u.Solved,
                u.Best > 0 ? u.Best : null
            );
        }).ToList();
    }

    public async Task<ChallengeDetailsDto?> GetByIdAsync(int challengeId, int? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var c = await _db.Challenges.AsNoTracking()
            .Where(x => x.ChallengeId == challengeId && x.IsPublished && !x.IsDeleted)
            .Select(x => new
            {
                x.ChallengeId,
                x.Title,
                x.Description,
                x.Requirements,
                x.StarterCode,
                Difficulty = x.Difficulty.ToString(),
                x.MaxPoints,
                x.CreatedAt,
                ProblemCode = x.Problem.ProblemCode,
                x.KnowledgeArticleId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (c == null) return null;

        bool solved = false;
        int? best = null;
        if (currentUserId.HasValue)
        {
            var attempts = await _db.ChallengeAttempts.AsNoTracking()
                .Where(a => a.ChallengeId == challengeId && a.UserId == currentUserId.Value && a.Evaluation != null)
                .Select(a => new { a.Evaluation!.IsPassed, a.Evaluation.PointsAwarded })
                .ToListAsync(cancellationToken);
            if (attempts.Count > 0)
            {
                solved = attempts.Any(a => a.IsPassed);
                best = attempts.Max(a => a.PointsAwarded);
            }
        }

        return new ChallengeDetailsDto(
            c.ChallengeId,
            c.Title,
            c.Description,
            c.Requirements,
            c.StarterCode,
            c.Difficulty,
            c.MaxPoints,
            c.CreatedAt,
            c.ProblemCode,
            c.KnowledgeArticleId,
            solved,
            best
        );
    }

    public async Task<ChallengeEvaluationResultDto> SubmitSolutionAsync(
        int challengeId,
        int userId,
        SubmitChallengeSolutionDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.SubmittedCode))
        {
            throw AppError.InvalidRequest("Submitted code is required.", "INVALID_REQUEST", nameof(dto.SubmittedCode));
        }

        var challenge = await _db.Challenges
            .Include(c => c.Problem)
            .FirstOrDefaultAsync(c => c.ChallengeId == challengeId && c.IsPublished && !c.IsDeleted, cancellationToken)
            ?? throw AppError.NotFound($"Challenge {challengeId} was not found.");

        var attempt = new ChallengeAttempt
        {
            ChallengeId = challengeId,
            UserId = userId,
            SubmittedCode = dto.SubmittedCode.Trim(),
            Status = "Submitted",
            StartedAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow
        };
        _db.ChallengeAttempts.Add(attempt);
        await _db.SaveChangesAsync(cancellationToken);

        var systemPrompt = $$"""
            You are DevPilot AI grading a coding challenge solution.
            Return ONE JSON object only (no markdown fences):
            {
              "isPassed": true,
              "correctnessScore": 0,
              "codeQualityScore": 0,
              "securityScore": 0,
              "pointsAwarded": 0,
              "feedbackSummary": "short overall feedback",
              "advice": "practical advice to improve the code / approach",
              "detailedReport": "longer explanation of what was good and what to improve"
            }
            Rules:
            - Scores are integers 0-100.
            - pointsAwarded must be between 0 and {{challenge.MaxPoints}}.
            - Award points based on difficulty and solution quality.
            - If the bug is not fixed, isPassed=false and pointsAwarded should be low (0-20% of max).
            - If partially fixed, award partial points.
            - Be fair and educational.
            """;

        var userPrompt = $"""
            Challenge title: {challenge.Title}
            Difficulty: {challenge.Difficulty}
            Max points: {challenge.MaxPoints}
            Requirements:
            {challenge.Requirements}

            Starter (buggy) code:
            {challenge.StarterCode}

            Developer submitted solution:
            {dto.SubmittedCode}

            Related original bug title: {challenge.Problem.Title}
            """;

        var evaluationCost = _creditCostService != null
            ? await _creditCostService.GetCostAsync("ChallengeEvaluation", cancellationToken)
            : CreditCosts.ChallengeEvaluation;

        if (_planUsageService != null)
        {
            await _planUsageService.EnsureCreditsAsync(userId, evaluationCost, cancellationToken);
        }

        var ai = await _aiGateway.ExecuteAsync(new AIGatewayRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            TaskType: "ChallengeEvaluation",
            UserId: userId,
            ProblemId: challenge.ProblemId,
            Purpose: $"Evaluate challenge #{challengeId} attempt",
            MaxTokens: 2048,
            Temperature: 0.2
        ), cancellationToken);

        if (!ai.IsSuccess)
        {
            attempt.Status = "Failed";
            await _db.SaveChangesAsync(cancellationToken);
            throw AppError.ServiceUnavailable(ai.ErrorMessage ?? "Challenge evaluation failed.", "AI_GATEWAY_UNAVAILABLE");
        }

        if (_planUsageService != null)
        {
            await _planUsageService.ChargeCreditsAsync(
                userId,
                evaluationCost,
                $"Challenge evaluation #{challengeId}",
                referenceId: $"challenge-eval:{challengeId}:{attempt.ChallengeAttemptId}",
                aiUsageEventId: ai.AIUsageEventId,
                metadataJson: JsonSerializer.Serialize(new
                {
                    taskType = "ChallengeEvaluation",
                    challengeId,
                    attempt.ChallengeAttemptId,
                    aiRequestId = ai.AIRequestId,
                    provider = ai.ProviderName
                }),
                tokensUsed: ai.TotalTokens,
                cancellationToken: cancellationToken);
        }

        var graded = ParseEvaluation(ai.Content, challenge.MaxPoints);
        var evaluation = new ChallengeEvaluation
        {
            ChallengeAttemptId = attempt.ChallengeAttemptId,
            CorrectnessScore = graded.CorrectnessScore,
            CodeQualityScore = graded.CodeQualityScore,
            SecurityScore = graded.SecurityScore,
            PointsAwarded = graded.PointsAwarded,
            IsPassed = graded.IsPassed,
            FeedbackSummary = graded.FeedbackSummary,
            Advice = graded.Advice,
            DetailedReport = graded.DetailedReport,
            EvaluatedAt = DateTime.UtcNow
        };

        attempt.Status = graded.IsPassed ? "Passed" : "Reviewed";
        _db.ChallengeEvaluations.Add(evaluation);
        await _db.SaveChangesAsync(cancellationToken);

        if (_auditService != null)
        {
            await _auditService.LogAsync(
                SystemAuditEventTypes.ChallengeEvaluated,
                nameof(ChallengeAttempt),
                attempt.ChallengeAttemptId.ToString(),
                oldValues: null,
                newValues: new
                {
                    challengeId,
                    attempt.ChallengeAttemptId,
                    evaluation.IsPassed,
                    evaluation.PointsAwarded,
                    ai.AIRequestId,
                    ai.AIUsageEventId,
                    ai.ProviderName
                },
                userId: userId,
                cancellationToken: cancellationToken);
        }

        return new ChallengeEvaluationResultDto(
            attempt.ChallengeAttemptId,
            challenge.ChallengeId,
            evaluation.IsPassed,
            evaluation.CorrectnessScore,
            evaluation.CodeQualityScore,
            evaluation.SecurityScore,
            evaluation.PointsAwarded,
            challenge.MaxPoints,
            evaluation.FeedbackSummary,
            evaluation.Advice,
            evaluation.DetailedReport,
            ai.ProviderName
        );
    }

    public async Task<MyChallengeStatsDto> GetLeaderboardAsync(int? currentUserId = null, int top = 10, CancellationToken cancellationToken = default)
    {
        if (top < 1 || top > 50) top = 10;

        // Best points per user per challenge, then sum.
        var bestPerChallenge = await _db.ChallengeAttempts.AsNoTracking()
            .Where(a => a.Evaluation != null)
            .GroupBy(a => new { a.UserId, a.ChallengeId })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.ChallengeId,
                BestPoints = g.Max(x => x.Evaluation!.PointsAwarded),
                Passed = g.Any(x => x.Evaluation!.IsPassed)
            })
            .ToListAsync(cancellationToken);

        var aggregated = bestPerChallenge
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalPoints = g.Sum(x => x.BestPoints),
                ChallengesSolved = g.Count(x => x.Passed)
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.ChallengesSolved)
            .ToList();

        var userIds = aggregated.Select(x => x.UserId).ToList();
        var names = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.Name, u.Email })
            .ToListAsync(cancellationToken);
        var nameMap = names.ToDictionary(
            u => u.UserId,
            u => string.IsNullOrWhiteSpace(u.Name) ? (u.Email ?? $"User {u.UserId}") : u.Name);

        var ranked = aggregated.Select((x, index) => new LeaderboardEntryDto(
            Rank: index + 1,
            UserId: x.UserId,
            DisplayName: nameMap.TryGetValue(x.UserId, out var n) ? n : $"User {x.UserId}",
            TotalPoints: x.TotalPoints,
            ChallengesSolved: x.ChallengesSolved
        )).ToList();

        int? myRank = null;
        var myPoints = 0;
        var mySolved = 0;
        if (currentUserId.HasValue)
        {
            var mine = ranked.FirstOrDefault(r => r.UserId == currentUserId.Value);
            if (mine != null)
            {
                myRank = mine.Rank;
                myPoints = mine.TotalPoints;
                mySolved = mine.ChallengesSolved;
            }
        }

        return new MyChallengeStatsDto(
            TotalPoints: myPoints,
            ChallengesSolved: mySolved,
            Rank: myRank,
            Top: ranked.Take(top).ToList()
        );
    }

    private static int MaxPointsFor(ChallengeDifficulty difficulty) => difficulty switch
    {
        ChallengeDifficulty.Beginner => 50,
        ChallengeDifficulty.Advanced => 150,
        _ => 100
    };

    private static ChallengeDifficulty ParseDifficulty(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "beginner" => ChallengeDifficulty.Beginner,
            "advanced" => ChallengeDifficulty.Advanced,
            _ => ChallengeDifficulty.Intermediate
        };

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? value : (value.Length <= max ? value.Trim() : value.Trim()[..max]);

    private static GeneratedChallenge ParseGeneratedChallenge(string raw)
    {
        var json = ExtractJsonObject(raw);
        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var parsed = new GeneratedChallenge
                {
                    Title = root.TryGetProperty("title", out var title) ? title.GetString() : null,
                    Description = root.TryGetProperty("description", out var description) ? description.GetString() : null,
                    StarterCode = root.TryGetProperty("starterCode", out var starter) ? starter.GetString() : null,
                    Difficulty = root.TryGetProperty("difficulty", out var difficulty) ? difficulty.GetString() : null,
                    Requirements = ExtractRequirementsProperty(root)
                };
                return parsed;
            }
            catch (JsonException) { }
        }

        return new GeneratedChallenge
        {
            Title = "Practice bug fix",
            Description = "Fix the bug in the starter code.",
            Requirements = "Fix the bug in the starter code and make the behavior correct.",
            StarterCode = raw.Length > 500 ? raw[..500] : raw,
            Difficulty = "Intermediate"
        };
    }

    private static string? ExtractRequirementsProperty(JsonElement root)
    {
        if (!root.TryGetProperty("requirements", out var requirements))
        {
            return null;
        }

        if (requirements.ValueKind == JsonValueKind.Array)
        {
            var items = requirements.EnumerateArray()
                .Select(item => item.GetString()?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();
            return items.Count == 0 ? null : string.Join('\n', items);
        }

        return requirements.GetString();
    }

    private static string FormatRequirementsList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Fix the bug in the starter code and make the behavior correct.";
        }

        var text = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (text.Contains('\n', StringComparison.Ordinal))
        {
            return string.Join(
                '\n',
                text.Split('\n')
                    .Select(static line => RequirementsItemRegex.Replace(line.Trim(), string.Empty).Trim())
                    .Where(static line => line.Length > 0));
        }

        var numbered = RequirementsSplitRegex.Split(text)
            .Select(static part => RequirementsItemRegex.Replace(part.Trim(), string.Empty).Trim())
            .Where(static part => part.Length > 0)
            .ToList();

        if (numbered.Count >= 2)
        {
            return string.Join('\n', numbered);
        }

        return text;
    }

    private static GradedChallenge ParseEvaluation(string? raw, int maxPoints)
    {
        var json = ExtractJsonObject(raw ?? string.Empty);
        GradedChallenge graded = new();
        if (json != null)
        {
            try
            {
                graded = JsonSerializer.Deserialize<GradedChallenge>(json, JsonOptions) ?? new GradedChallenge();
            }
            catch (JsonException)
            {
                graded = new GradedChallenge
                {
                    FeedbackSummary = "Could not parse structured evaluation.",
                    DetailedReport = raw ?? string.Empty,
                    Advice = "Review the requirements and try again."
                };
            }
        }

        graded.CorrectnessScore = Clamp(graded.CorrectnessScore, 0, 100);
        graded.CodeQualityScore = Clamp(graded.CodeQualityScore, 0, 100);
        graded.SecurityScore = Clamp(graded.SecurityScore, 0, 100);

        if (graded.PointsAwarded <= 0)
        {
            var weighted = (graded.CorrectnessScore * 0.6) + (graded.CodeQualityScore * 0.25) + (graded.SecurityScore * 0.15);
            graded.PointsAwarded = (int)Math.Round(maxPoints * (weighted / 100.0));
        }

        graded.PointsAwarded = Clamp(graded.PointsAwarded, 0, maxPoints);
        if (!graded.IsPassed && graded.CorrectnessScore >= 70)
        {
            graded.IsPassed = true;
        }

        if (string.IsNullOrWhiteSpace(graded.FeedbackSummary))
        {
            graded.FeedbackSummary = graded.IsPassed ? "Solid solution." : "The solution needs more work.";
        }

        if (string.IsNullOrWhiteSpace(graded.Advice))
        {
            graded.Advice = "Compare your fix against the challenge requirements and edge cases.";
        }

        return graded;
    }

    private static string? ExtractJsonObject(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}')) return trimmed;
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start) return trimmed[start..(end + 1)];
        return null;
    }

    private static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    private sealed class GeneratedChallenge
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Requirements { get; set; }
        public string? StarterCode { get; set; }
        public string? Difficulty { get; set; }
    }

    private sealed class GradedChallenge
    {
        public bool IsPassed { get; set; }
        public int CorrectnessScore { get; set; }
        public int CodeQualityScore { get; set; }
        public int SecurityScore { get; set; }
        public int PointsAwarded { get; set; }
        public string FeedbackSummary { get; set; } = string.Empty;
        public string Advice { get; set; } = string.Empty;
        public string DetailedReport { get; set; } = string.Empty;
    }
}
