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
        return Ok(result);
    }

    public record FeedbackRequestDto(int AttemptId, bool IsSolved, string? Comments);

    [HttpPost("{problemId:int}/feedback")]
    public async Task<IActionResult> SubmitFeedback(int problemId, [FromBody] FeedbackRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await _problemService.SubmitFeedbackAsync(problemId, dto.AttemptId, dto.IsSolved, dto.Comments, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{problemId:int}")]
    public async Task<IActionResult> GetProblem(int problemId, CancellationToken cancellationToken)
    {
        var problem = await _problemService.GetProblemDetailsAsync(problemId, cancellationToken);
        if (problem == null) return NotFound();
        return Ok(problem);
    }
}
