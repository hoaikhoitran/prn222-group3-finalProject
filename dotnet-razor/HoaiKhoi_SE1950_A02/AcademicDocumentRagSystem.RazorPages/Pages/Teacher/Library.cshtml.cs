using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Teacher;

[SessionAuthorize("Teacher")]
public class LibraryModel : PageModel
{
    private readonly IDocumentService _documentService;

    public LibraryModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public List<DocumentListItemDto> Documents { get; private set; } = new();

    public string? FilterCourse { get; private set; }

    public string? SearchQuery { get; private set; }

    public List<string> AllCourseCodes { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(string? course, string? q)
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        var all = await _documentService.GetByTeacherAsync(accountId.Value);
        var docs = all.AsEnumerable();

        FilterCourse = course;
        SearchQuery = q;

        if (!string.IsNullOrWhiteSpace(course))
        {
            docs = docs.Where(d => string.Equals(d.CourseCode, course, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            docs = docs.Where(d => d.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (d.Chapter?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.OriginalFileName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        AllCourseCodes = all.Select(d => d.CourseCode).Distinct().OrderBy(c => c).ToList();
        Documents = docs.ToList();
        return Page();
    }
}
