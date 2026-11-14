using DevPilotAI.Application.Problems;
using Xunit;

namespace DevPilotAI.Tests;

public class DebuggingResultParserTests
{
    [Fact]
    public void ParseOrFallback_ValidJson_ReturnsStructuredFieldsInOrder()
    {
        var raw = """
        {
          "problem": "Null reference",
          "location": "UserService.cs:42",
          "rootCause": "User was null",
          "solutionSummary": "Add a null check",
          "steps": [
            { "order": 2, "title": "Return", "detail": "Return NotFound when null" },
            { "order": 1, "title": "Guard", "detail": "Check user == null" }
          ],
          "correctedCode": "if (user is null) return NotFound();",
          "notes": "Keep tests green"
        }
        """;

        var payload = DebuggingResultParser.ParseOrFallback(raw, "Fallback title");
        var dto = DebuggingResultParser.ToDto(1, "DEV-000001", 9, "Gemini", payload);

        Assert.Equal("Null reference", dto.Problem);
        Assert.Equal("UserService.cs:42", dto.Location);
        Assert.Equal("User was null", dto.RootCause);
        Assert.Equal("Add a null check", dto.SolutionSummary);
        Assert.Equal(2, dto.Steps.Count);
        Assert.Equal(1, dto.Steps[0].Order);
        Assert.Equal("Guard", dto.Steps[0].Title);
        Assert.Equal(2, dto.Steps[1].Order);
        Assert.Contains("user is null", dto.CorrectedCode);
        Assert.Equal("Keep tests green", dto.Notes);
        Assert.Equal("Gemini", dto.ProviderUsed);
        Assert.Contains("1. Guard:", dto.ProposedSolution);
    }

    [Fact]
    public void ParseOrFallback_InvalidText_UsesSafeFallback()
    {
        var payload = DebuggingResultParser.ParseOrFallback("just plain text advice", "My bug");
        var dto = DebuggingResultParser.ToDto(2, "DEV-000002", 3, "Groq", payload);

        Assert.Equal("My bug", dto.Problem);
        Assert.NotEmpty(dto.RootCause);
        Assert.Single(dto.Steps);
        Assert.Contains("just plain text advice", dto.Steps[0].Detail);
        Assert.Equal("Structured parse fallback was used.", dto.Notes);
    }

    [Fact]
    public void ParseOrFallback_JsonInsideMarkdownFence_StillParses()
    {
        var raw = """
        ```json
        {
          "problem": "Build failed",
          "location": "csproj",
          "rootCause": "Wrong TFM",
          "solutionSummary": "Retarget net9.0",
          "steps": [{ "order": 1, "title": "Update", "detail": "Set TargetFramework to net9.0" }],
          "correctedCode": "<TargetFramework>net9.0</TargetFramework>",
          "notes": ""
        }
        ```
        """;

        var payload = DebuggingResultParser.ParseOrFallback(raw);
        Assert.Equal("Build failed", payload.Problem);
        Assert.Equal("Wrong TFM", payload.RootCause);
    }

    [Fact]
    public void FromPersistedAttempt_RebuildsCorrectedCodeAndSteps()
    {
        var explanation = "Null reference in GetTotal\nLocation: OrderService.GetTotal — line 14";
        var proposed = """
            Initialize the dictionary before use.

            1. Add constructor: Create OrderService() and assign _prices.
            2. Use TryGetValue: Avoid indexer crashes on missing keys.

            Corrected code:
            public class OrderService
            {
                private readonly Dictionary<string, decimal> _prices = new();
            }
            """;

        var payload = DebuggingResultParser.FromPersistedAttempt(
            explanation,
            "Field was never initialized",
            proposed,
            "Fallback");

        Assert.Equal("Null reference in GetTotal", payload.Problem);
        Assert.Equal("OrderService.GetTotal — line 14", payload.Location);
        Assert.Equal("Field was never initialized", payload.RootCause);
        Assert.Contains("Initialize the dictionary", payload.SolutionSummary);
        Assert.Equal(2, payload.Steps!.Count);
        Assert.Contains("Dictionary<string, decimal>", payload.CorrectedCode);

        var article = DebuggingResultParser.BuildArticleMarkdown("How to fix NRE", payload, "C#");
        Assert.Contains("## What went wrong", article);
        Assert.Contains("## Corrected code", article);
        Assert.Contains("```", article);
    }
}
