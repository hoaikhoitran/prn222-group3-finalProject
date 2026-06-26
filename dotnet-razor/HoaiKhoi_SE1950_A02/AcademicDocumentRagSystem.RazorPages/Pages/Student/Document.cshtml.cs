using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Student;

[SessionAuthorize("Student")]
public class DocumentModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly IChatService _chatService;

    public DocumentModel(IDocumentService documentService, IChatService chatService)
    {
        _documentService = documentService;
        _chatService = chatService;
    }

    public DocumentDetailsDto Document { get; private set; } = new();

    public DocumentChunkDto? ActiveChunk { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? id, int? chunk)
    {
        if (!id.HasValue)
        {
            return RedirectToPage("/Student/Library");
        }

        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        ViewData["RecentSessions"] = await _chatService.GetSessionsAsync(accountId.Value);

        var details = await _documentService.GetDetailsAsync(id.Value, accountId, "Student");
        if (details == null)
        {
            return NotFound();
        }

        Document = details;

        if (Document.Chunks.Count > 0)
        {
            var ordered = Document.Chunks.OrderBy(c => c.ChunkIndex).ToList();
            ActiveChunk = chunk.HasValue
                ? ordered.FirstOrDefault(c => c.ChunkIndex == chunk.Value) ?? ordered[0]
                : ordered[0];
        }

        return Page();
    }

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:0.#} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }
}
