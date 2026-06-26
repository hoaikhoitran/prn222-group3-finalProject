using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Documents;

[SessionAuthorize("Admin", "Teacher")]
public class DetailsModel : PageModel
{
    private readonly IDocumentService _documentService;

    public DetailsModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public DocumentDetailsDto Details { get; private set; } = new();

    public string BackPage { get; private set; } = "/Documents/All";

    public string BackLabel { get; private set; } = "← Tất cả tài liệu";

    public string RoleName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        var roleName = HttpContext.Session.GetString(SessionKeys.RoleName) ?? string.Empty;
        RoleName = roleName;

        var details = await _documentService.GetDetailsAsync(id, accountId, roleName);
        if (details == null)
        {
            return RedirectToPage("/Auth/AccessDenied");
        }

        Details = details;
        if (roleName == "Teacher")
        {
            BackPage = "/Teacher/IndexStatus";
            BackLabel = "← Trạng thái index";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostReIndexAsync(int id)
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        var email = HttpContext.Session.GetString(SessionKeys.Email) ?? string.Empty;
        var roleName = HttpContext.Session.GetString(SessionKeys.RoleName) ?? string.Empty;

        try
        {
            await _documentService.ReIndexAsync(id, accountId, email, roleName);
            TempData["Success"] = "Đã re-index tài liệu.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }
}
