using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Problems;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Domain.Enums;
using DevPilotAI.Infrastructure.AIGateway;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class SimilarityEngineTests
{
    private DevPilotDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DevPilotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new DevPilotDbContext(options);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ShouldProduceConsistentNonEmptyVector()
    {
        using var context = GetInMemoryDbContext();
        var engine = new SimilarityEngine(context, NullLogger<SimilarityEngine>.Instance);

        var text = "NullReferenceException in UserAuthController line 45";
        var embedding1 = await engine.GenerateEmbeddingAsync(text);
        var embedding2 = await engine.GenerateEmbeddingAsync(text);

        Assert.NotEmpty(embedding1);
        Assert.Equal(embedding1.Length, embedding2.Length);
        Assert.Equal(1024, embedding1.Length);
        Assert.Equal(embedding1, embedding2);
    }

    [Fact]
    public async Task CosineSimilarity_IdenticalText_ShouldEqualOne()
    {
        using var context = GetInMemoryDbContext();
        var engine = new SimilarityEngine(context, NullLogger<SimilarityEngine>.Instance);

        var text = "SqlException timeout connecting to database pool";
        var emb1 = await engine.GenerateEmbeddingAsync(text);
        var emb2 = await engine.GenerateEmbeddingAsync(text);

        var score = engine.CalculateCosineSimilarity(emb1, emb2);

        Assert.InRange(score, 0.999, 1.0001);
    }

    [Fact]
    public async Task CosineSimilarity_HighlySimilarProblems_ShouldScoreAboveThreshold()
    {
        using var context = GetInMemoryDbContext();
        var engine = new SimilarityEngine(context, NullLogger<SimilarityEngine>.Instance);

        var textA = "JWT Bearer token 401 Unauthorized in ASP.NET Core API pipeline";
        var textB = "ASP.NET Core API returns 401 Unauthorized for JWT Bearer token";

        var embA = await engine.GenerateEmbeddingAsync(textA);
        var embB = await engine.GenerateEmbeddingAsync(textB);

        var score = engine.CalculateCosineSimilarity(embA, embB);

        Assert.True(score > 0.85, $"Expected similarity score > 0.85, but was {score}");
    }

    [Fact]
    public async Task FindBestMatchAsync_WhenSolvedProblemExists_ReturnsVerifiedMatch()
    {
        using var context = GetInMemoryDbContext();
        var engine = new SimilarityEngine(context, NullLogger<SimilarityEngine>.Instance);

        var solvedProblem = new Problem
        {
            ProblemCode = "DEV-000100",
            UserId = 1,
            Title = "JWT 401 Unauthorized in ASP.NET Core",
            Summary = "Authentication failed with 401",
            Status = ProblemStatus.Solved,
            ResolvedAt = DateTime.UtcNow
        };
        context.Problems.Add(solvedProblem);
        await context.SaveChangesAsync();

        var solvedRequest = new ProblemRequest
        {
            ProblemId = solvedProblem.ProblemId,
            Description = "Getting 401 Unauthorized when sending JWT token",
            ErrorMessage = "401 Unauthorized",
            Framework = "ASP.NET Core",
            CreatedAt = DateTime.UtcNow
        };
        context.ProblemRequests.Add(solvedRequest);
        await context.SaveChangesAsync();

        var solvedEmb = await engine.GenerateEmbeddingAsync(
            $"{solvedProblem.Title}\n{solvedRequest.Framework}\n{solvedRequest.Description}\n{solvedRequest.ErrorMessage}");

        context.ProblemEmbeddings.Add(new ProblemEmbedding
        {
            ProblemRequestId = solvedRequest.ProblemRequestId,
            EmbeddingData = solvedEmb,
            ModelVersion = "feature-v1",
            CreatedAt = DateTime.UtcNow
        });

        context.ProblemSolutions.Add(new ProblemSolution
        {
            ProblemId = solvedProblem.ProblemId,
            SuccessfulAttemptId = 1,
            RootCause = "Missing app.UseAuthentication() before app.UseAuthorization()",
            Summary = "Fix middleware pipeline order",
            DetailedSteps = "1. Place app.UseAuthentication() before app.UseAuthorization().\n2. Rebuild and run.",
            SolvedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var newQuery = "401 Unauthorized error with JWT token in ASP.NET Core";
        var match = await engine.FindBestMatchAsync(
            currentRequestId: 999,
            currentText: newQuery,
            framework: "ASP.NET Core",
            threshold: 0.80);

        Assert.NotNull(match);
        Assert.Equal(solvedProblem.ProblemId, match.MatchedProblemId);
        Assert.Equal("DEV-000100", match.MatchedProblemCode);
        Assert.Contains("UseAuthentication", match.ProvenSolution);
        Assert.True(match.SimilarityScore >= 0.80);
    }

    [Fact]
    public async Task SubmitProblemAsync_WithHighSimilarity_TriggersKnowledgeAdaptationAndLogsMatch()
    {
        using var context = GetInMemoryDbContext();
        var engine = new SimilarityEngine(context, NullLogger<SimilarityEngine>.Instance);

        var solvedProblem = new Problem
        {
            ProblemCode = "DEV-000001",
            UserId = 1,
            Title = "CORS policy blocking frontend requests",
            Summary = "Access-Control-Allow-Origin missing",
            Status = ProblemStatus.Solved,
            ResolvedAt = DateTime.UtcNow
        };
        context.Problems.Add(solvedProblem);
        await context.SaveChangesAsync();

        var solvedRequest = new ProblemRequest
        {
            ProblemId = solvedProblem.ProblemId,
            Description = "Browser blocks fetch due to CORS origin policy missing header",
            ErrorMessage = "CORS policy: No 'Access-Control-Allow-Origin' header is present",
            Framework = "ASP.NET Core",
            CreatedAt = DateTime.UtcNow
        };
        context.ProblemRequests.Add(solvedRequest);
        await context.SaveChangesAsync();

        var text = $"{solvedProblem.Title}\n{solvedRequest.Framework}\n{solvedRequest.Description}\n{solvedRequest.ErrorMessage}";
        var emb = await engine.GenerateEmbeddingAsync(text);
        context.ProblemEmbeddings.Add(new ProblemEmbedding
        {
            ProblemRequestId = solvedRequest.ProblemRequestId,
            EmbeddingData = emb,
            ModelVersion = "feature-v1",
            CreatedAt = DateTime.UtcNow
        });

        context.ProblemSolutions.Add(new ProblemSolution
        {
            ProblemId = solvedProblem.ProblemId,
            SuccessfulAttemptId = 1,
            RootCause = "CORS middleware missing or placed after routing",
            Summary = "Configure CORS policy",
            DetailedSteps = "Add builder.Services.AddCors(...) and app.UseCors().",
            SolvedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        AIGatewayRequest? capturedRequest = null;
        var mockAiGateway = new Mock<IAIGateway>();
        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AIGatewayRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new AIGatewayResponse(
                IsSuccess: true,
                Content: """
                {
                  "problem": "CORS blocked",
                  "location": "Program.cs CORS middleware",
                  "rootCause": "Missing Access-Control-Allow-Origin",
                  "solutionSummary": "Add CORS policy before routing.",
                  "steps": [
                    { "order": 1, "title": "Enable CORS", "detail": "Call UseCors before MapControllers." }
                  ],
                  "correctedCode": "app.UseCors();",
                  "notes": ""
                }
                """,
                ProviderName: "Gemini",
                ModelName: "gemini-flash",
                PromptTokens: 25,
                CompletionTokens: 30,
                DurationMs: 80
            ));

        var problemService = new ProblemService(context, mockAiGateway.Object, engine);

        var newProblemDto = new CreateProblemDto(
            UserId: 2,
            Title: "CORS policy blocking frontend requests",
            Description: "Browser blocks fetch due to CORS origin policy missing header",
            ErrorMessage: "CORS policy: No 'Access-Control-Allow-Origin' header is present",
            StackTrace: null,
            SourceCode: "fetch('https://api.devpilot.ai');",
            Logs: null,
            Framework: "ASP.NET Core"
        );

        var result = await problemService.SubmitProblemAsync(newProblemDto);

        Assert.NotNull(result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("KnowledgeAdaptation", capturedRequest.TaskType);
        Assert.Contains("DEV-000001", capturedRequest.UserPrompt);
        Assert.Contains("Reused Knowledge from DEV-000001", result.RootCause);

        var recordedMatch = await context.SimilarityMatches.FirstOrDefaultAsync();
        Assert.NotNull(recordedMatch);
        Assert.Equal(solvedProblem.ProblemId, recordedMatch.MatchedProblemId);
        Assert.True(recordedMatch.SimilarityScore >= 0.85m);

        var embeddingRecord = await context.ProblemEmbeddings
            .FirstOrDefaultAsync(e => e.ProblemRequestId != solvedRequest.ProblemRequestId);
        Assert.NotNull(embeddingRecord);
        Assert.NotEmpty(embeddingRecord.EmbeddingData);
    }
}
