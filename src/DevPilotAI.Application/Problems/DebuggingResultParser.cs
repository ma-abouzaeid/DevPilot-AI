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

    /// <summary>
    /// Rebuilds structured fields from persisted attempt columns.
    /// ProposedSolution is human-readable text (not AI JSON), so ParseOrFallback must not be used on it.
    /// </summary>
    public static StructuredDebugPayload FromPersistedAttempt(
        string? explanation,
        string? rootCause,
        string? proposedSolution,
        string? fallbackProblemTitle = null)
    {
        var payload = new StructuredDebugPayload
        {
            RootCause = string.IsNullOrWhiteSpace(rootCause) ? null : rootCause.Trim(),
            Steps = []
        };

        if (!string.IsNullOrWhiteSpace(explanation))
        {
            var lines = explanation.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var locationIndex = Array.FindIndex(
                lines,
                static line => line.StartsWith("Location:", StringComparison.OrdinalIgnoreCase));

            if (locationIndex >= 0)
            {
                payload.Location = lines[locationIndex]["Location:".Length..].Trim();
                payload.Problem = string.Join('\n', lines.Take(locationIndex)).Trim();
            }
            else
            {
                payload.Problem = explanation.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(payload.Problem))
        {
            payload.Problem = string.IsNullOrWhiteSpace(fallbackProblemTitle)
                ? "Developer problem"
                : fallbackProblemTitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(proposedSolution))
        {
            var text = proposedSolution.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
            const string codeMarker = "Corrected code:";
            var codeIndex = text.IndexOf(codeMarker, StringComparison.OrdinalIgnoreCase);
            var body = text;
            if (codeIndex >= 0)
            {
                payload.CorrectedCode = text[(codeIndex + codeMarker.Length)..].Trim();
                body = text[..codeIndex].Trim();
            }

            var summaryLines = new List<string>();
            var steps = new List<StructuredDebugStep>();
            var inSteps = false;

            foreach (var rawLine in body.Split('\n'))
            {
                var line = rawLine.TrimEnd();
                var match = PersistedStepRegex().Match(line.Trim());
                if (match.Success)
                {
                    inSteps = true;
                    steps.Add(new StructuredDebugStep
                    {
                        Order = int.Parse(match.Groups[1].Value),
                        Title = match.Groups[2].Value.Trim(),
                        Detail = match.Groups[3].Value.Trim()
                    });
                    continue;
                }

                if (!inSteps)
                {
                    summaryLines.Add(line);
                }
                else if (steps.Count > 0 && !string.IsNullOrWhiteSpace(line))
                {
                    var last = steps[^1];
                    last.Detail = string.IsNullOrWhiteSpace(last.Detail)
                        ? line.Trim()
                        : $"{last.Detail} {line.Trim()}";
                }
            }

            payload.SolutionSummary = string.Join('\n', summaryLines).Trim();
            payload.Steps = steps;
        }

        if (string.IsNullOrWhiteSpace(payload.Location))
        {
            payload.Location = "See the submitted source and stack context.";
        }

        if (string.IsNullOrWhiteSpace(payload.RootCause))
        {
            payload.RootCause = "Review the persisted analysis for this solved attempt.";
        }

        if (string.IsNullOrWhiteSpace(payload.SolutionSummary))
        {
            payload.SolutionSummary = "Apply the persisted fix steps below.";
        }

        return payload;
    }

    public static string BuildArticleMarkdown(
        string title,
        StructuredDebugPayload payload,
        string? framework = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## What went wrong");
        sb.AppendLine(payload.Problem?.Trim() ?? title);
        sb.AppendLine();

        sb.AppendLine("## Where it happened");
        sb.AppendLine(string.IsNullOrWhiteSpace(payload.Location)
            ? "Location could not be determined precisely."
            : payload.Location.Trim());
        if (!string.IsNullOrWhiteSpace(framework))
        {
            sb.AppendLine();
            sb.AppendLine($"**Stack / framework:** {framework.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine("## Why it happened");
        sb.AppendLine(payload.RootCause?.Trim() ?? "Root cause analysis was not available.");
        sb.AppendLine();

        sb.AppendLine("## How to fix it");
        sb.AppendLine(payload.SolutionSummary?.Trim() ?? "Follow the steps below.");
        sb.AppendLine();

        var steps = payload.Steps ?? [];
        if (steps.Count > 0)
        {
            sb.AppendLine("## Fix steps");
            foreach (var step in steps.OrderBy(s => s.Order))
            {
                var stepTitle = string.IsNullOrWhiteSpace(step.Title) ? "Step" : step.Title.Trim();
                var detail = string.IsNullOrWhiteSpace(step.Detail) ? "" : step.Detail.Trim();
                sb.AppendLine($"{Math.Max(1, step.Order)}. **{stepTitle}** — {detail}");
            }

            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(payload.CorrectedCode))
        {
            sb.AppendLine("## Corrected code");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(payload.CorrectedCode.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## Key takeaway");
        sb.AppendLine(string.IsNullOrWhiteSpace(payload.Notes)
            ? "Initialize required state before use, and prefer safe lookups over unchecked indexing."
            : payload.Notes.Trim());

        return sb.ToString().Trim();
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
          "location": "Class.Method or file area, and include the line number like 'line 14' whenever you can infer it from the submitted code",
          "rootCause": "the root cause in plain language",
          "solutionSummary": "one short paragraph summarizing the fix",
          "steps": [
            { "order": 1, "title": "short step title", "detail": "what to do in this step" }
          ],
          "correctedCode": "the full corrected code, properly formatted with newlines and indentation (never minify onto one line). Empty string if not applicable.",
          "notes": "optional extra tips or empty string"
        }
        Rules:
        - Always include all keys.
        - steps must be an ordered array with at least one item.
        - Keep explanations practical and developer-friendly.
        - Prefer concrete fixes over generic advice.
        - Infer title and framework even when the user did not provide them.
        - For location, always try to name the method/class AND the approximate line number from the submitted source (e.g. "OrderService.GetTotal — line 14 (_prices[id])").
        - correctedCode must be readable multi-line source with normal indentation.
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

    [GeneratedRegex(@"^(\d+)\.\s*([^:]+):\s*(.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex PersistedStepRegex();
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
