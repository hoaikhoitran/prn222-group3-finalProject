using AcademicDocumentRagSystem.Services.DTOs.Chat;

namespace AcademicDocumentRagSystem.Services.Interfaces;

public interface IChatService
{
    Task<List<IndexedDocumentDto>> GetIndexedDocumentsAsync();

    Task<List<ChatSessionDto>> GetSessionsAsync(int accountId);

    Task<ChatSessionDetailsDto?> GetSessionAsync(int chatSessionId, int accountId);

    Task<ChatAnswerDto> AskAsync(AskQuestionDto dto, int accountId);

    Task<ChatWorkspaceDto> GetWorkspaceAsync(int accountId, int? documentId, int? sessionId);
}
