using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DevPilotAI.Application.Problems.DTOs;

namespace DevPilotAI.Application.Problems;

public static partial class DebuggingResultParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static DebuggingResultDto ToDto(
        int problemId,
        string problemCode,
        int debuggingAttemptId,
        string providerUsed,
        StructuredDebugPayload payload)
    {
        var steps = (payload.Steps ?? [])
            .Select((s, i) => new DebugStepDto(
                Order: s.Order > 0 ? s.Order : i + 1,
                Title: string.IsNullOrWhiteSpace(s.Title) ? $"Step {i + 1}" : s.Title.Trim(),
                Detail: (s.Detail ?? string.Empty).Trim()))
            .OrderBy(s => s.Order)
            .ToList();

        if (steps.Count == 0 && !string.IsNullOrWhiteSpace(payload.SolutionSummary))
        {
            steps.Add(new DebugStepDto(1, "Apply the fix", payload.SolutionSummary.Trim()));
        }

        var proposed = BuildProposedSolutionText(payload, steps);

        return new DebuggingResultDto(
            ProblemId: problemId,
            ProblemCode: problemCode,
            DebuggingAttemptId: debuggingAttemptId,
            Problem: (payload.Problem ?? string.Empty).Trim(),
            Location: (payload.Location ?? string.Empty).Trim(),
            RootCause: (payload.RootCause ?? string.Empty).Trim(),
            SolutionSummary: (payload.SolutionSummary ?? string.Empty).Trim(),
            Steps: steps,
            CorrectedCode: (payload.CorrectedCode ?? string.Empty).Trim(),
            Notes: string.IsNullOrWhiteSpace(payload.Notes) ? null : payload.Notes.Trim(),
            ProviderUsed: providerUsed,
            ProposedSolution: proposed
        );
    }

    public static StructuredDebugPayload ParseOrFallback(string? rawContent, string? fallbackProblemTitle = null)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return CreateFallback(fallbackProblemTitle, "The AI provider returned an empty response.");
        }

        var trimmed = rawContent.Trim();
        var jsonCandidate = ExtractJsonObject(trimmed);

        if (jsonCandidate != null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<StructuredDebugPayload>(jsonCandidate, JsonOptions);
                if (parsed != null && HasUsefulContent(parsed))
                {
                    return Normalize(parsed, fallbackProblemTitle);
                }
            }
            catch (JsonException)
            {
                // fall through to text fallback
            }
        }

        return CreateFallback(fallbackProblemTitle, trimmed);
    }

    public static string BuildSystemPrompt() =>
        """
        You are DevPilot AI, an expert software debugging assistant.
        Analyze the developer's problem and respond with ONE JSON object only.
        Do not wrap the JSON in markdown fences. Do not add any text before or after the JSON.
        The developer usually only provides code + error message (+ optional notes).
        You must infer a clear title and framework/stack yourself and fill every field.
        Use this exact schema and key order:
        {
          "title": "short searchable problem title (max ~80 chars)",
          "framework": "detected language/framework/stack or empty string",
          "problem": "short clear description of the problem",
          "location": "where the issue is (file/function/line/area if known, otherwise best guess)",
          "rootCause": "the root cause in plain language",
          "solutionSummary": "one short paragraph summarizing the fix",
          "steps": [
            { "order": 1, "title": "short step title", "detail": "what to do in this step" }
          ],
          "correctedCode": "the corrected code snippet or empty string if not applicable",
          "notes": "optional extra tips or empty string"
        }
        Rules:
        - Always include all keys.
        - steps must be an ordered array with at least one item.
        - Keep explanations practical and developer-friendly.
        - Prefer concrete fixes over generic advice.
        - Infer title and framework even when the user did not provide them.
        """;

    public static string BuildUserPrompt(
        string title,
        string? framework,
        string description,
        string? errorMessage,
        string? stackTrace,
        string? sourceCode,
        string? logs = null,
        string? existingSolution = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(existingSolution))
        {
            sb.AppendLine("[EXISTING VERIFIED SOLUTION TO ADAPT]");
            sb.AppendLine(existingSolution.Trim());
            sb.AppendLine();
            sb.AppendLine("[CURRENT DEVELOPER CONTEXT]");
        }

        sb.AppendLine("The user submitted a minimal bug report. Infer title, framework, and missing context.");
        if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine($"Working title: {title}");
        if (!string.IsNullOrWhiteSpace(framework)) sb.AppendLine($"Framework hint: {framework}");
        if (!string.IsNullOrWhiteSpace(description)) sb.AppendLine($"Notes: {description}");
        if (!string.IsNullOrWhiteSpace(errorMessage)) sb.AppendLine($"Error: {errorMessage}");
        if (!string.IsNullOrWhiteSpace(stackTrace)) sb.AppendLine($"StackTrace: {stackTrace}");
        if (!string.IsNullOrWhiteSpace(sourceCode)) sb.AppendLine($"Code:\n{sourceCode}");
        if (!string.IsNullOrWhiteSpace(logs)) sb.AppendLine($"Logs:\n{logs}");
        sb.AppendLine();
        sb.AppendLine("Return the fixed JSON schema only.");
        return sb.ToString();
    }

    private static bool HasUsefulContent(StructuredDebugPayload payload) =>
        !string.IsNullOrWhiteSpace(payload.Problem)
        || !string.IsNullOrWhiteSpace(payload.RootCause)
        || !string.IsNullOrWhiteSpace(payload.SolutionSummary)
        || !string.IsNullOrWhiteSpace(payload.CorrectedCode)
        || (payload.Steps?.Count > 0);

    private static StructuredDebugPayload Normalize(StructuredDebugPayload payload, string? fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(payload.Problem))
        {
            payload.Problem = fallbackTitle ?? "Developer problem";
        }

        payload.Steps ??= [];
        return payload;
    }

    private static StructuredDebugPayload CreateFallback(string? title, string raw)
    {
        return new StructuredDebugPayload
        {
            Problem = title ?? "Developer problem",
            Location = "Unable to determine exact location from the model response.",
            RootCause = "The model did not return valid structured JSON. Showing raw analysis.",
            SolutionSummary = "Review the raw analysis below and apply the suggested fix carefully.",
            Steps =
            [
                new StructuredDebugStep
                {
                    Order = 1,
                    Title = "Review AI analysis",
                    Detail = raw.Length > 4000 ? raw[..4000] : raw
                }
            ],
            CorrectedCode = string.Empty,
            Notes = "Structured parse fallback was used."
        };
    }

    private static string? ExtractJsonObject(string text)
    {
        if (text.StartsWith('{') && text.EndsWith('}'))
        {
            return text;
        }

        var fenced = JsonFenceRegex().Match(text);
        if (fenced.Success)
        {
            return fenced.Groups[1].Value.Trim();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return text[start..(end + 1)];
        }

        return null;
    }

    private static string BuildProposedSolutionText(StructuredDebugPayload payload, IReadOnlyList<DebugStepDto> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine(payload.SolutionSummary?.Trim() ?? string.Empty);
        sb.AppendLine();
        foreach (var step in steps)
        {
            sb.AppendLine($"{step.Order}. {step.Title}: {step.Detail}");
        }

        if (!string.IsNullOrWhiteSpace(payload.CorrectedCode))
        {
            sb.AppendLine();
            sb.AppendLine("Corrected code:");
            sb.AppendLine(payload.CorrectedCode.Trim());
        }

        return sb.ToString().Trim();
    }

    [GeneratedRegex(@"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonFenceRegex();
}

public class StructuredDebugPayload
{
    public string? Title { get; set; }
    public string? Framework { get; set; }
    public string? Problem { get; set; }
    public string? Location { get; set; }
    public string? RootCause { get; set; }
    public string? SolutionSummary { get; set; }
    public List<StructuredDebugStep>? Steps { get; set; }
    public string? CorrectedCode { get; set; }
    public string? Notes { get; set; }
}

public class StructuredDebugStep
{
    public int Order { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
}
