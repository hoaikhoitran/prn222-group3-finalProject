using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Teacher;

[SessionAuthorize("Teacher")]
public class UploadModel : PageModel
{
    private readonly IDocumentService _documentService;

    public UploadModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [BindProperty]
    public DocumentUploadDto Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        var courseId = HttpContext.Session.GetInt32(SessionKeys.CourseId);
        var courseCode = HttpContext.Session.GetString(SessionKeys.CourseCode);

        if (accountId == null || courseId == null || string.IsNullOrWhiteSpace(courseCode))
        {
            return RedirectToPage("/Auth/AccessDenied");
        }

        Input.CourseId = courseId.Value;
        Input.CourseCode = courseCode;
        Input.AvailableCourses = await _documentService.GetUploadCoursesForTeacherAsync(accountId.Value);
        if (Input.AvailableCourses.Count == 1)
        {
            Input.CourseId = Input.AvailableCourses[0].CourseId;
            Input.CourseCode = Input.AvailableCourses[0].Code;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        var email = HttpContext.Session.GetString(SessionKeys.Email);

        if (accountId == null || string.IsNullOrWhiteSpace(email))
        {
            return RedirectToPage("/Auth/Login");
        }

        if (!ModelState.IsValid)
        {
            Input.AvailableCourses = await _documentService.GetUploadCoursesForTeacherAsync(accountId.Value);
            return Page();
        }

        try
        {
            var documentId = await _documentService.UploadAndIndexAsync(Input, accountId.Value, email);
            TempData["Success"] = "Tài liệu đã upload và đang được index.";
            return RedirectToPage("/Teacher/IndexStatus", new { highlight = documentId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            Input.AvailableCourses = await _documentService.GetUploadCoursesForTeacherAsync(accountId.Value);
            return Page();
        }
    }
}
