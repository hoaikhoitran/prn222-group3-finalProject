using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.RazorPages.ViewModels;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Teacher;

[SessionAuthorize("Teacher")]
public class CoursesModel : PageModel
{
    private readonly IDocumentService _documentService;

    public CoursesModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public List<TeacherCourseCardViewModel> Cards { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        Cards = await BuildCourseCardsAsync(accountId.Value);
        return Page();
    }

    public async Task<IActionResult> OnGetTableAsync()
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return Unauthorized();
        }

        Cards = await BuildCourseCardsAsync(accountId.Value);
        return Partial("_CourseTable", Cards);
    }

    private async Task<List<TeacherCourseCardViewModel>> BuildCourseCardsAsync(int accountId)
    {
        var courses = await _documentService.GetUploadCoursesForTeacherAsync(accountId);
        var documents = await _documentService.GetByTeacherAsync(accountId);

        return courses.Select(c => new TeacherCourseCardViewModel
        {
            Course = c,
            DocumentCount = documents.Count(d => d.CourseCode == c.Code),
            IndexedCount = documents.Count(d => d.CourseCode == c.Code && d.IndexStatus == "Indexed"),
            TotalChunks = documents.Where(d => d.CourseCode == c.Code).Sum(d => d.TotalChunks)
        }).ToList();
    }
}
