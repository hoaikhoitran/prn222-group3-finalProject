using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Teacher;

[SessionAuthorize("Teacher")]
public class UploadModel : PageModel
{
    private const string DuplicateTitleMessage =
        "Tiêu đề này đã tồn tại với chương đang chọn trong môn học.";

    private const string DuplicateChapterMessage =
        "Chương này đã tồn tại với tiêu đề đang chọn trong môn học.";

    private readonly IDocumentService _documentService;

    public UploadModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [BindProperty]
    public DocumentUploadDto Input { get; set; } = new();

    /// <summary>JSON [{ courseId, title, chapter }] for FE duplicate checks (case-sensitive).</summary>
    public string ExistingTitleChapterJson { get; private set; } = "[]";

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

        await LoadExistingTitleChaptersAsync(accountId.Value);
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

        Input.AvailableCourses = await _documentService.GetUploadCoursesForTeacherAsync(accountId.Value);
        await LoadExistingTitleChaptersAsync(accountId.Value);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (HasDuplicateTitleChapter())
        {
            ModelState.AddModelError("Input.Title", DuplicateTitleMessage);
            ModelState.AddModelError("Input.Chapter", DuplicateChapterMessage);
            return Page();
        }

        try
        {
            var documentId = await _documentService.UploadAndIndexAsync(Input, accountId.Value, email);
            TempData["Success"] = "Tài liệu đã upload và index xong.";
            return RedirectToPage("/Teacher/IndexStatus", new { highlight = documentId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadExistingTitleChaptersAsync(accountId.Value);
            return Page();
        }
    }

    private bool HasDuplicateTitleChapter()
    {
        var course = Input.AvailableCourses.FirstOrDefault(c => c.CourseId == Input.CourseId);
        if (course == null || string.IsNullOrEmpty(Input.Title))
        {
            return false;
        }

        var chapter = Input.Chapter ?? string.Empty;
        var courseCode = course.Code;

        return _cachedDocuments.Any(d =>
            !string.Equals(d.UploadStatus, "Deleted", StringComparison.OrdinalIgnoreCase)
            && string.Equals(d.CourseCode, courseCode, StringComparison.Ordinal)
            && string.Equals(d.Title, Input.Title, StringComparison.Ordinal)
            && string.Equals(d.Chapter ?? string.Empty, chapter, StringComparison.Ordinal));
    }

    private List<DocumentListItemDto> _cachedDocuments = new();

    private async Task LoadExistingTitleChaptersAsync(int accountId)
    {
        _cachedDocuments = await _documentService.GetByTeacherAsync(accountId);

        var courseIdByCode = Input.AvailableCourses
            .GroupBy(c => c.Code)
            .ToDictionary(g => g.Key, g => g.First().CourseId);

        var keys = _cachedDocuments
            .Where(d => !string.Equals(d.UploadStatus, "Deleted", StringComparison.OrdinalIgnoreCase))
            .Select(d =>
            {
                courseIdByCode.TryGetValue(d.CourseCode, out var mappedCourseId);
                return new
                {
                    courseId = mappedCourseId,
                    title = d.Title ?? string.Empty,
                    chapter = d.Chapter ?? string.Empty
                };
            })
            .Where(k => k.courseId > 0);

        ExistingTitleChapterJson = JsonSerializer.Serialize(keys)
            .Replace("<", "\\u003c", StringComparison.Ordinal);
    }
}
