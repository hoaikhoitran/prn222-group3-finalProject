namespace AcademicDocumentRagSystem.Services.DTOs.Chat;

public class ChatWorkspaceDto
{
    public List<ChatSessionDto> Sessions { get; set; } = new();

    public List<IndexedDocumentDto> Documents { get; set; } = new();

    public ChatSessionDetailsDto? ActiveSession { get; set; }

    public IndexedDocumentDto? ActiveDocument { get; set; }

    public int? SelectedDocumentId { get; set; }

    public int? SelectedSessionId { get; set; }

    public AskQuestionDto AskForm { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public bool ShowDocumentPicker { get; set; }
}
