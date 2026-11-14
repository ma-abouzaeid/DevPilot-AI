using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Problems;
using DevPilotAI.Application.Problems.DTOs;
using DevPilotAI.Domain.Entities;
using DevPilotAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class ProblemServiceTests
{
    private DevPilotDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DevPilotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new DevPilotDbContext(options);
    }

    [Fact]
    public async Task SubmitProblemAsync_ShouldCreateProblemAndInitialAttempt()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();

        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(
                IsSuccess: true,
                Content: """
                {
                  "problem": "JWT Auth Failed",
                  "location": "AuthHandler.cs:20",
                  "rootCause": "Authentication middleware missing",
                  "solutionSummary": "Register authentication before authorization.",
                  "steps": [
                    { "order": 1, "title": "Add UseAuthentication", "detail": "Call app.UseAuthentication() before UseAuthorization()." }
                  ],
                  "correctedCode": "app.UseAuthentication();\napp.UseAuthorization();",
                  "notes": ""
                }
                """,
                ProviderName: "Groq",
                ModelName: "llama-3",
                PromptTokens: 10,
                CompletionTokens: 20,
                DurationMs: 100
            ));

        var service = new ProblemService(dbContext, mockAiGateway.Object);

        var dto = new CreateProblemDto(
            UserId: 1,
            Title: "JWT Auth Failed",
            Description: "Returns 401 Unauthorized",
            ErrorMessage: "401 Unauthorized",
            StackTrace: "at AuthHandler.cs:20",
            SourceCode: "app.UseAuthentication();",
            Logs: "Log item",
            Framework: "ASP.NET Core"
        );

        var result = await service.SubmitProblemAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("DEV-000001", result.ProblemCode);
        Assert.Equal("JWT Auth Failed", result.Problem);
        Assert.Equal("AuthHandler.cs:20", result.Location);
        Assert.Equal("Authentication middleware missing", result.RootCause);
        Assert.Equal("Register authentication before authorization.", result.SolutionSummary);
        Assert.Single(result.Steps);
        Assert.Contains("UseAuthentication", result.CorrectedCode);
        Assert.Equal("Groq", result.ProviderUsed);

        var dbProblem = await dbContext.Problems.FirstOrDefaultAsync(p => p.ProblemId == result.ProblemId);
        Assert.NotNull(dbProblem);
        Assert.Equal("JWT Auth Failed", dbProblem.Title);
    }

    [Fact]
    public async Task SubmitProblemAsync_MissingSourceCode_ShouldThrowArgumentException()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();
        var service = new ProblemService(dbContext, mockAiGateway.Object);

        var dto = new CreateProblemDto(
            UserId: 1,
            Title: null,
            Description: null,
            ErrorMessage: "NullReferenceException",
            StackTrace: null,
            SourceCode: "",
            Logs: null,
            Framework: null
        );

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitProblemAsync(dto));
    }

    [Fact]
    public async Task SubmitProblemAsync_MinimalCodeAndError_ShouldInferTitleFromAi()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();

        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(
                IsSuccess: true,
                Content: """
                {
                  "title": "Null reference on user profile",
                  "framework": "ASP.NET Core",
                  "problem": "User profile is null when accessed",
                  "location": "ProfileService.cs",
                  "rootCause": "Missing null check",
                  "solutionSummary": "Guard against null profile.",
                  "steps": [
                    { "order": 1, "title": "Add null guard", "detail": "Return NotFound when profile is null." }
                  ],
                  "correctedCode": "if (user?.Profile is null) return NotFound();",
                  "notes": ""
                }
                """,
                ProviderName: "Groq",
                ModelName: "llama-3",
                PromptTokens: 10,
                CompletionTokens: 20,
                DurationMs: 80
            ));

        var service = new ProblemService(dbContext, mockAiGateway.Object);

        var dto = new CreateProblemDto(
            UserId: 1,
            Title: null,
            Description: null,
            ErrorMessage: "NullReferenceException: Object reference not set",
            SourceCode: "var name = user.Profile.Name;"
        );

        var result = await service.SubmitProblemAsync(dto);

        Assert.Equal("User profile is null when accessed", result.Problem);
        var dbProblem = await dbContext.Problems.FirstAsync(p => p.ProblemId == result.ProblemId);
        Assert.Equal("Null reference on user profile", dbProblem.Title);
        var request = await dbContext.ProblemRequests.FirstAsync(r => r.ProblemId == result.ProblemId);
        Assert.Equal("ASP.NET Core", request.Framework);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_WhenSolved_ShouldMarkProblemSolvedAndCreateArticle()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();

        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(true, "Solution Steps", "Gemini", "gemini-flash", 10, 10, 50));

        var service = new ProblemService(dbContext, mockAiGateway.Object);

        var createDto = new CreateProblemDto(1, "NullRef Exception", "Object null", "NullReferenceException", null, "var x = obj.Value;", null, "C#");
        var submitted = await service.SubmitProblemAsync(createDto);

        var feedbackResult = await service.SubmitFeedbackAsync(submitted.ProblemId, submitted.DebuggingAttemptId, isSolved: true, comments: "Works great");

        Assert.NotNull(feedbackResult);

        var problem = await dbContext.Problems.FirstOrDefaultAsync(p => p.ProblemId == submitted.ProblemId);
        Assert.NotNull(problem);
        Assert.Equal(DevPilotAI.Domain.Enums.ProblemStatus.Solved, problem.Status);

        var article = await dbContext.KnowledgeArticles.FirstOrDefaultAsync(a => a.ProblemId == submitted.ProblemId);
        Assert.NotNull(article);
        Assert.Equal("ART-DEV-000001", article.ArticleCode);
    }

    [Fact]
    public async Task GetProblemHistoryAsync_ShouldReturnAllRequestsAndAttempts()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();

        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(true, "Solution Steps", "Gemini", "gemini-flash", 10, 10, 50));

        var service = new ProblemService(dbContext, mockAiGateway.Object);

        var createDto = new CreateProblemDto(1, "CORS Error", "Blocked by CORS", "CORS Error", null, "fetch('/api')", null, "ASP.NET");
        var submitted = await service.SubmitProblemAsync(createDto);

        var history = await service.GetProblemHistoryAsync(submitted.ProblemId);

        Assert.NotNull(history);
        Assert.Single(history.Requests);
        Assert.Single(history.Attempts);
        Assert.Equal("DEV-000001", history.ProblemCode);
    }

    [Fact]
    public async Task FollowUpProblemAsync_ShouldAddSecondAttemptOnSameProblem()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();
        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(
                true,
                """{"title":"Follow-up","framework":"C#","problem":"Still failing","location":"line 2","rootCause":"Bad null check","solutionSummary":"Fix again","steps":[{"order":1,"title":"Retry","detail":"Apply guard"}],"correctedCode":"if(x is null) return;","notes":""}""",
                "Groq", "llama", 10, 10, 40));

        var service = new ProblemService(dbContext, mockAiGateway.Object);
        var submitted = await service.SubmitProblemAsync(new CreateProblemDto(
            1, null, null, "NullRef", null, "var x = a.B;", null, null));

        var followUp = await service.FollowUpProblemAsync(
            submitted.ProblemId,
            new FollowUpProblemDto("Still NullRef", "var x = a?.B;", "Tried null-conditional"),
            userId: 1);

        Assert.Equal(submitted.ProblemId, followUp.ProblemId);
        Assert.Equal(submitted.ProblemCode, followUp.ProblemCode);
        Assert.Equal(2, await dbContext.DebuggingAttempts.CountAsync(a => a.ProblemId == submitted.ProblemId));
        Assert.Equal(2, await dbContext.ProblemRequests.CountAsync(r => r.ProblemId == submitted.ProblemId));
    }

    [Fact]
    public async Task GetMyProblemsDashboardAsync_ShouldCountOnlyCurrentUser()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();
        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(true, """{"problem":"p","location":"l","rootCause":"r","solutionSummary":"s","steps":[{"order":1,"title":"t","detail":"d"}],"correctedCode":"","notes":""}""", "Groq", "m", 1, 1, 1));

        var service = new ProblemService(dbContext, mockAiGateway.Object);
        var mine = await service.SubmitProblemAsync(new CreateProblemDto(7, null, null, "err-a", null, "code-a", null, null));
        await service.SubmitProblemAsync(new CreateProblemDto(99, null, null, "err-b", null, "code-b", null, null));
        await service.SubmitFeedbackAsync(mine.ProblemId, mine.DebuggingAttemptId, true, "ok", userId: 7);

        var dashboard = await service.GetMyProblemsDashboardAsync(7);

        Assert.Equal(1, dashboard.TotalCount);
        Assert.Equal(1, dashboard.SolvedCount);
        Assert.Equal(0, dashboard.OpenCount);
        Assert.Single(dashboard.Recent);
    }

    [Fact]
    public async Task SearchPublishedArticlesAsync_ShouldReturnOnlyPublishedSolvedArticles()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();
        mockAiGateway
            .Setup(g => g.ExecuteAsync(It.IsAny<AIGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIGatewayResponse(true, """{"title":"Auth fix","framework":"ASP.NET","problem":"401","location":"Auth","rootCause":"order","solutionSummary":"reorder","steps":[{"order":1,"title":"Fix","detail":"UseAuthentication first"}],"correctedCode":"app.UseAuthentication();","notes":""}""", "Groq", "m", 1, 1, 1));

        var service = new ProblemService(dbContext, mockAiGateway.Object);
        var open = await service.SubmitProblemAsync(new CreateProblemDto(1, null, null, "open-err", null, "open-code", null, null));
        var solved = await service.SubmitProblemAsync(new CreateProblemDto(1, null, null, "solved-err", null, "solved-code", null, null));
        await service.SubmitFeedbackAsync(solved.ProblemId, solved.DebuggingAttemptId, true, "works", userId: 1);

        var articles = await service.SearchPublishedArticlesAsync(null, null);
        Assert.Equal(1, articles.TotalCount);
        Assert.Contains("How to fix", articles.Items[0].Title);

        var details = await service.GetPublishedArticleAsync(problemId: solved.ProblemId);
        Assert.NotNull(details);
        Assert.False(string.IsNullOrWhiteSpace(details.ContentMarkdown));

        var missing = await service.GetPublishedArticleAsync(problemId: open.ProblemId);
        Assert.Null(missing);
    }
}
