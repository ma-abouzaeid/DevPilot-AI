using DevPilotAI.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevPilotAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProblemsController : ControllerBase
{
    private readonly IProblemService _problemService;

    public ProblemsController(IProblemService problemService)
    {
        _problemService = problemService;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitProblem([FromBody] CreateProblemDto dto, CancellationToken cancellationToken)
    {
        var result = await _problemService.SubmitProblemAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetProblem), new { problemId = result.ProblemId }, new { success = true, data = result });
    }

    [HttpPost("{problemId:int}/requests")]
    public async Task<IActionResult> AddProblemRequest(int problemId, [FromBody] AddProblemRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _problemService.AddProblemRequestAsync(problemId, dto, cancellationToken);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("{problemId:int}/debug")]
    public async Task<IActionResult> DebugProblem(int problemId, [FromQuery] int requestId, CancellationToken cancellationToken)
    {
        var result = await _problemService.DebugProblemAsync(problemId, requestId, cancellationToken);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("{problemId:int}/attempts/{attemptId:int}/feedback")]
    public async Task<IActionResult> SubmitFeedback(int problemId, int attemptId, [FromBody] FeedbackRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _problemService.SubmitFeedbackAsync(problemId, attemptId, dto.IsSolved, dto.Comments, cancellationToken);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("{problemId:int}")]
    public async Task<IActionResult> GetProblem(int problemId, CancellationToken cancellationToken)
    {
        var problem = await _problemService.GetProblemDetailsAsync(problemId, cancellationToken);
        if (problem == null)
        {
            return NotFound(new { success = false, errorCode = "NOT_FOUND", message = $"Problem with ID {problemId} not found." });
        }
        return Ok(new { success = true, data = problem });
    }

    [HttpGet("code/{problemCode}")]
    public async Task<IActionResult> GetProblemByCode(string problemCode, CancellationToken cancellationToken)
    {
        var problem = await _problemService.GetProblemByCodeAsync(problemCode, cancellationToken);
        if (problem == null)
        {
            return NotFound(new { success = false, errorCode = "NOT_FOUND", message = $"Problem with code {problemCode} not found." });
        }
        return Ok(new { success = true, data = problem });
    }

    [HttpGet("{problemId:int}/history")]
    public async Task<IActionResult> GetProblemHistory(int problemId, CancellationToken cancellationToken)
    {
        var history = await _problemService.GetProblemHistoryAsync(problemId, cancellationToken);
        return Ok(new { success = true, data = history });
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchProblems(
        [FromQuery] string? q,
        [FromQuery] string? technology,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _problemService.SearchProblemsAsync(q, technology, page, pageSize, cancellationToken);
        return Ok(new { success = true, data = result });
    }
}
