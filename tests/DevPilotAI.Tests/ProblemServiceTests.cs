using DevPilotAI.Application.Common.Interfaces;
using DevPilotAI.Application.Services;
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
                Content: "Test Solution",
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
        Assert.Equal("Test Solution", result.ProposedSolution);
        Assert.Equal("Groq", result.ProviderUsed);

        var dbProblem = await dbContext.Problems.FirstOrDefaultAsync(p => p.ProblemId == result.ProblemId);
        Assert.NotNull(dbProblem);
        Assert.Equal("JWT Auth Failed", dbProblem.Title);
    }

    [Fact]
    public async Task SubmitProblemAsync_EmptyTitle_ShouldThrowArgumentException()
    {
        var dbContext = GetInMemoryDbContext();
        var mockAiGateway = new Mock<IAIGateway>();
        var service = new ProblemService(dbContext, mockAiGateway.Object);

        var dto = new CreateProblemDto(
            UserId: 1,
            Title: "",
            Description: "Valid description",
            ErrorMessage: null,
            StackTrace: null,
            SourceCode: null,
            Logs: null,
            Framework: null
        );

        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitProblemAsync(dto));
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

        var createDto = new CreateProblemDto(1, "NullRef Exception", "Object null", "NullReferenceException", null, null, null, "C#");
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

        var createDto = new CreateProblemDto(1, "CORS Error", "Blocked by CORS", "CORS Error", null, null, null, "ASP.NET");
        var submitted = await service.SubmitProblemAsync(createDto);

        var history = await service.GetProblemHistoryAsync(submitted.ProblemId);

        Assert.NotNull(history);
        Assert.Single(history.Requests);
        Assert.Single(history.Attempts);
        Assert.Equal("DEV-000001", history.ProblemCode);
    }
}
