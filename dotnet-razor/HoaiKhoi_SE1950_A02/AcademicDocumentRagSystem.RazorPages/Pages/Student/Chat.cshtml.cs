using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Chat;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Student;

[SessionAuthorize("Student")]
public class ChatModel : PageModel
{
    private readonly IChatService _chatService;

    public ChatModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public ChatWorkspaceDto Workspace { get; private set; } = new();

    [BindProperty]
    public AskQuestionDto AskForm { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? courseCode, int? documentId, int? sessionId)
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        Workspace = await _chatService.GetWorkspaceAsync(accountId.Value, documentId, sessionId);

        // Landing page: students choose a course first, then a document.
        if (!documentId.HasValue && !sessionId.HasValue)
        {
            Workspace.SelectedDocumentId = null;
            Workspace.SelectedSessionId = null;
            Workspace.ActiveDocument = null;
            Workspace.ActiveSession = null;
            Workspace.AskForm = new AskQuestionDto();
        }

        ApplyCourseSelection(courseCode, documentId, sessionId);
        Workspace.ShowDocumentPicker = !string.IsNullOrWhiteSpace(Workspace.SelectedCourseCode);

        Workspace.SuccessMessage = TempData["Success"] as string;
        Workspace.ErrorMessage = TempData["Error"] as string;
        AskForm = Workspace.AskForm;
        ViewData["RecentSessions"] = Workspace.Sessions;
        ViewData["SelectedSessionId"] = Workspace.SelectedSessionId;
        return Page();
    }

    public IActionResult OnGetNewChat(int documentId)
    {
        return RedirectToPage(new { documentId });
    }

    public async Task<IActionResult> OnPostAskAsync()
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        if (!ModelState.IsValid)
        {
            Workspace = await _chatService.GetWorkspaceAsync(
                accountId.Value, AskForm.DocumentId, AskForm.ChatSessionId);
            ApplyCourseSelection(AskForm.CourseCode, AskForm.DocumentId, AskForm.ChatSessionId);
            Workspace.AskForm = AskForm;
            Workspace.ErrorMessage = "Vui lòng nhập câu hỏi.";
            ViewData["RecentSessions"] = Workspace.Sessions;
            ViewData["SelectedSessionId"] = Workspace.SelectedSessionId;
            return Page();
        }

        try
        {
            var result = await _chatService.AskAsync(AskForm, accountId.Value);
            return RedirectToPage(new { courseCode = result.CourseCode, sessionId = result.ChatSessionId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage(new { courseCode = AskForm.CourseCode, sessionId = AskForm.ChatSessionId });
        }
    }

    private void ApplyCourseSelection(string? courseCode, int? documentId, int? sessionId)
    {
        var allDocuments = Workspace.Documents;
        Workspace.CourseCodes = allDocuments
            .Select(d => d.CourseCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        var selectedCourseCode = courseCode;

        if (string.IsNullOrWhiteSpace(selectedCourseCode) && Workspace.ActiveDocument != null)
        {
            selectedCourseCode = Workspace.ActiveDocument.CourseCode;
        }

        if (string.IsNullOrWhiteSpace(selectedCourseCode) && sessionId.HasValue && Workspace.ActiveSession != null)
        {
            selectedCourseCode = Workspace.ActiveSession.CourseCode;
        }

        Workspace.SelectedCourseCode = selectedCourseCode;
        Workspace.AskForm.CourseCode = selectedCourseCode;

        if (!string.IsNullOrWhiteSpace(selectedCourseCode))
        {
            Workspace.Documents = allDocuments
                .Where(d => string.Equals(d.CourseCode, selectedCourseCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Workspace.ActiveDocument ??= Workspace.Documents.FirstOrDefault();
        }
    }
}
