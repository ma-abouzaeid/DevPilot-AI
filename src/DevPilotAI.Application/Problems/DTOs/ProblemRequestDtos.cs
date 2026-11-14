using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Problems.DTOs;

/// <summary>
/// Minimal user input: code + error are required. Title/framework/etc. are optional —
/// the AI infers and persists them during analysis.
/// </summary>
public record CreateProblemDto(
    int UserId,

    /// <summary>Optional. When omitted, AI generates a searchable title from the error/code.</summary>
    [StringLength(200, ErrorMessage = "Problem title cannot exceed 200 characters.")]
    [property: StringLength(200, ErrorMessage = "Problem title cannot exceed 200 characters.")]
    string? Title = null,

    /// <summary>Optional context from the developer.</summary>
    [StringLength(10000, ErrorMessage = "Problem description cannot exceed 10000 characters.")]
    [property: StringLength(10000, ErrorMessage = "Problem description cannot exceed 10000 characters.")]
    string? Description = null,

    [Required(ErrorMessage = "Error message is required.")]
    [MinLength(2, ErrorMessage = "Error message must be at least 2 characters.")]
    [StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    [property: Required(ErrorMessage = "Error message is required.")]
    [property: MinLength(2, ErrorMessage = "Error message must be at least 2 characters.")]
    [property: StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    string ErrorMessage = "",

    [StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    [property: StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    string? StackTrace = null,

    [Required(ErrorMessage = "Source code is required.")]
    [MinLength(3, ErrorMessage = "Source code must be at least 3 characters.")]
    [StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    [property: Required(ErrorMessage = "Source code is required.")]
    [property: MinLength(3, ErrorMessage = "Source code must be at least 3 characters.")]
    [property: StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    string SourceCode = "",

    [StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    [property: StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    string? Logs = null,

    [StringLength(100, ErrorMessage = "Framework cannot exceed 100 characters.")]
    [property: StringLength(100, ErrorMessage = "Framework cannot exceed 100 characters.")]
    string? Framework = null
);

public record AddProblemRequestDto(
    [Required(ErrorMessage = "Request content is required.")]
    [StringLength(10000, MinimumLength = 5, ErrorMessage = "Request content must be between 5 and 10000 characters.")]
    [property: Required(ErrorMessage = "Request content is required.")]
    [property: StringLength(10000, MinimumLength = 5, ErrorMessage = "Request content must be between 5 and 10000 characters.")]
    string Content,

    [StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    [property: StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    string? ErrorMessage = null,

    [StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    [property: StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    string? StackTrace = null,

    [StringLength(100, ErrorMessage = "TechnologyHint cannot exceed 100 characters.")]
    [property: StringLength(100, ErrorMessage = "TechnologyHint cannot exceed 100 characters.")]
    string? TechnologyHint = null,

    [StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    [property: StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    string? SourceCode = null,

    [StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    [property: StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    string? Logs = null
);

/// <summary>
/// Continue debugging the same problem with updated code/error context.
/// </summary>
public record FollowUpProblemDto(
    [Required(ErrorMessage = "Error message is required.")]
    [MinLength(2, ErrorMessage = "Error message must be at least 2 characters.")]
    [StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    [property: Required(ErrorMessage = "Error message is required.")]
    [property: MinLength(2, ErrorMessage = "Error message must be at least 2 characters.")]
    [property: StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    string ErrorMessage = "",

    [Required(ErrorMessage = "Source code is required.")]
    [MinLength(3, ErrorMessage = "Source code must be at least 3 characters.")]
    [StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    [property: Required(ErrorMessage = "Source code is required.")]
    [property: MinLength(3, ErrorMessage = "Source code must be at least 3 characters.")]
    [property: StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    string SourceCode = "",

    [StringLength(10000, ErrorMessage = "Description cannot exceed 10000 characters.")]
    [property: StringLength(10000, ErrorMessage = "Description cannot exceed 10000 characters.")]
    string? Description = null
);

public record FeedbackRequestDto(
    [Range(1, int.MaxValue, ErrorMessage = "AttemptId must be a positive integer.")]
    [property: Range(1, int.MaxValue, ErrorMessage = "AttemptId must be a positive integer.")]
    int AttemptId,

    bool IsSolved,

    [StringLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    [property: StringLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    string? Comments = null
);
