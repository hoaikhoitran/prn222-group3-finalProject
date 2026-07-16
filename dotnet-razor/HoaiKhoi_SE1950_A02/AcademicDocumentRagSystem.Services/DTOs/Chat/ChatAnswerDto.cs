using AcademicDocumentRagSystem.Services.DTOs.Rag;

namespace AcademicDocumentRagSystem.Services.DTOs.Chat;

public class ChatAnswerDto
{
    public int ChatSessionId { get; set; }

    public int DocumentId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public List<RagSourceDto> Sources { get; set; } = new();
}
