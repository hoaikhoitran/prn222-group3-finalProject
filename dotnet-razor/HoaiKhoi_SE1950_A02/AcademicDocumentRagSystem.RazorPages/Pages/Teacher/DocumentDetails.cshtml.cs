using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Teacher;

[SessionAuthorize("Teacher")]
public class DocumentDetailsModel : PageModel
{
    public IActionResult OnGet(int id)
    {
        return RedirectToPage("/Documents/Details", new { id });
    }

    public IActionResult OnPostReIndex(int id)
    {
        return RedirectToPage("/Documents/Details", new { id });
    }
}
