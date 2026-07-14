using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Teacher;

[SessionAuthorize("Teacher")]
public class IndexStatusModel : PageModel
{
    private readonly IDocumentService _documentService;

    public IndexStatusModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public List<DocumentListItemDto> Documents { get; private set; } = new();

    public int? HighlightId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? highlight)
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        Documents = await _documentService.GetByTeacherAsync(accountId.Value);
        HighlightId = highlight;
        return Page();
    }

    public async Task<IActionResult> OnGetStatusesAsync()
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return Unauthorized();
        }

        var documents = await _documentService.GetByTeacherAsync(accountId.Value);
        var indexed = documents.Count(d => d.IndexStatus == "Indexed");
        var processing = documents.Count(d => d.IndexStatus != "Indexed" && d.IndexStatus != "Failed");

        return new JsonResult(new
        {
            documents = documents.Select(d => new
            {
                documentId = d.DocumentId,
                indexStatus = d.IndexStatus,
                totalChunks = d.TotalChunks,
                indexError = d.IndexError
            }),
            stats = new
            {
                total = documents.Count,
                indexed,
                processing,
                totalChunks = documents.Sum(d => d.TotalChunks)
            }
        });
    }

    public async Task<IActionResult> OnPostReIndexAsync(int id)
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        var email = HttpContext.Session.GetString(SessionKeys.Email) ?? string.Empty;

        try
        {
            await _documentService.ReIndexAsync(id, accountId, email, "Teacher");
            TempData["Success"] = "Đã re-index tài liệu.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { highlight = id });
    }
}
