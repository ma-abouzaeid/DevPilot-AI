using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Problems.DTOs;

public record CreateProblemDto(
    int UserId,
    [property: Required(ErrorMessage = "Problem title is required.")]
    [property: StringLength(200, MinimumLength = 3, ErrorMessage = "Problem title must be between 3 and 200 characters.")]
    string Title,

    [property: Required(ErrorMessage = "Problem description is required.")]
    [property: StringLength(10000, MinimumLength = 10, ErrorMessage = "Problem description must be between 10 and 10000 characters.")]
    string Description,

    [property: StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    string? ErrorMessage = null,

    [property: StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    string? StackTrace = null,

    [property: StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    string? SourceCode = null,

    [property: StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    string? Logs = null,

    [property: StringLength(100, ErrorMessage = "Framework cannot exceed 100 characters.")]
    string? Framework = null
);

public record AddProblemRequestDto(
    [property: Required(ErrorMessage = "Request content is required.")]
    [property: StringLength(10000, MinimumLength = 5, ErrorMessage = "Request content must be between 5 and 10000 characters.")]
    string Content,

    [property: StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    string? ErrorMessage = null,

    [property: StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    string? StackTrace = null,

    [property: StringLength(100, ErrorMessage = "TechnologyHint cannot exceed 100 characters.")]
    string? TechnologyHint = null
);

public record FeedbackRequestDto(
    [property: Range(1, int.MaxValue, ErrorMessage = "AttemptId must be a positive integer.")]
    int AttemptId,

    bool IsSolved,

    [property: StringLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    string? Comments = null
);
