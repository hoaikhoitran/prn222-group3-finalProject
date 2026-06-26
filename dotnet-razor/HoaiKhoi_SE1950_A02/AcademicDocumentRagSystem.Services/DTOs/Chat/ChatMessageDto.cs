using AcademicDocumentRagSystem.Services.DTOs.Rag;

namespace AcademicDocumentRagSystem.Services.DTOs.Chat;

public class ChatMessageDto
{
    public int ChatMessageId { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<RagSourceDto> Sources { get; set; } = new();
}
