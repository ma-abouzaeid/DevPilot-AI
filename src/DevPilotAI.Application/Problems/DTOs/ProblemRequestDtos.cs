using System.ComponentModel.DataAnnotations;

namespace DevPilotAI.Application.Problems.DTOs;

/// <summary>
/// Minimal user input: code + error are required. Title/framework/etc. are optional —
/// the AI infers and persists them during analysis.
/// </summary>
public class CreateProblemDto
{
    public int UserId { get; set; }

    /// <summary>Optional. When omitted, AI generates a searchable title from the error/code.</summary>
    [StringLength(200, ErrorMessage = "Problem title cannot exceed 200 characters.")]
    public string? Title { get; set; }

    /// <summary>Optional context from the developer.</summary>
    [StringLength(10000, ErrorMessage = "Problem description cannot exceed 10000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Error message is required.")]
    [MinLength(2, ErrorMessage = "Error message must be at least 2 characters.")]
    [StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    public string ErrorMessage { get; set; } = string.Empty;

    [StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    public string? StackTrace { get; set; }

    [Required(ErrorMessage = "Source code is required.")]
    [MinLength(3, ErrorMessage = "Source code must be at least 3 characters.")]
    [StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    public string SourceCode { get; set; } = string.Empty;

    [StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    public string? Logs { get; set; }

    [StringLength(100, ErrorMessage = "Framework cannot exceed 100 characters.")]
    public string? Framework { get; set; }
}

public class AddProblemRequestDto
{
    [Required(ErrorMessage = "Request content is required.")]
    [StringLength(10000, MinimumLength = 5, ErrorMessage = "Request content must be between 5 and 10000 characters.")]
    public string Content { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    public string? ErrorMessage { get; set; }

    [StringLength(20000, ErrorMessage = "StackTrace cannot exceed 20000 characters.")]
    public string? StackTrace { get; set; }

    [StringLength(100, ErrorMessage = "TechnologyHint cannot exceed 100 characters.")]
    public string? TechnologyHint { get; set; }

    [StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    public string? SourceCode { get; set; }

    [StringLength(20000, ErrorMessage = "Logs cannot exceed 20000 characters.")]
    public string? Logs { get; set; }
}

/// <summary>
/// Continue debugging the same problem with updated code/error context.
/// </summary>
public class FollowUpProblemDto
{
    [Required(ErrorMessage = "Error message is required.")]
    [MinLength(2, ErrorMessage = "Error message must be at least 2 characters.")]
    [StringLength(4000, ErrorMessage = "ErrorMessage cannot exceed 4000 characters.")]
    public string ErrorMessage { get; set; } = string.Empty;

    [Required(ErrorMessage = "Source code is required.")]
    [MinLength(3, ErrorMessage = "Source code must be at least 3 characters.")]
    [StringLength(50000, ErrorMessage = "SourceCode cannot exceed 50000 characters.")]
    public string SourceCode { get; set; } = string.Empty;

    [StringLength(10000, ErrorMessage = "Description cannot exceed 10000 characters.")]
    public string? Description { get; set; }
}

public class FeedbackRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "AttemptId must be a positive integer.")]
    public int AttemptId { get; set; }

    public bool IsSolved { get; set; }

    [StringLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string? Comments { get; set; }
}
