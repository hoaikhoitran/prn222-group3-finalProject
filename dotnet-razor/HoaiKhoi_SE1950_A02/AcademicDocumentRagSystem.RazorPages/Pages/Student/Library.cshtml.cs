using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Chat;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Student;

[SessionAuthorize("Student")]
public class LibraryModel : PageModel
{
    private readonly IChatService _chatService;

    public LibraryModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public List<IndexedDocumentDto> Documents { get; private set; } = new();

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

        ViewData["RecentSessions"] = await _chatService.GetSessionsAsync(accountId.Value);

        // Students are never assigned to courses: the library always shows
        // every indexed document, optionally narrowed by the course filter.
        var all = await _chatService.GetIndexedDocumentsAsync();

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
                || (d.Chapter?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        AllCourseCodes = all.Select(d => d.CourseCode).Distinct().OrderBy(c => c).ToList();
        Documents = docs.ToList();
        return Page();
    }
}
