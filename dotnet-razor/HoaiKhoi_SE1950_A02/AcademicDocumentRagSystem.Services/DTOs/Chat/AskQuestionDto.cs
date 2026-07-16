using System.ComponentModel.DataAnnotations;

namespace AcademicDocumentRagSystem.Services.DTOs.Chat;

public class AskQuestionDto
{
    public int? ChatSessionId { get; set; }

    public int? DocumentId { get; set; }

    public string? CourseCode { get; set; }

    [Required]
    public string Question { get; set; } = string.Empty;
}
