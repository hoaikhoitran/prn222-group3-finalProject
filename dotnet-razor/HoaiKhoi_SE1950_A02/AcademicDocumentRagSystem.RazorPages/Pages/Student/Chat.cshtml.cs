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

    public async Task<IActionResult> OnGetAsync(int? documentId, int? sessionId)
    {
        var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);
        if (accountId == null)
        {
            return RedirectToPage("/Auth/Login");
        }

        Workspace = await _chatService.GetWorkspaceAsync(accountId.Value, documentId, sessionId);

        // Legacy Chat/Index: show document picker when landing without explicit selection.
        if (!documentId.HasValue && !sessionId.HasValue)
        {
            Workspace.SelectedDocumentId = null;
            Workspace.SelectedSessionId = null;
            Workspace.ActiveDocument = null;
            Workspace.ActiveSession = null;
            Workspace.AskForm = new AskQuestionDto();
            Workspace.ShowDocumentPicker = true;
        }

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
            Workspace.AskForm = AskForm;
            Workspace.ErrorMessage = "Vui lòng nhập câu hỏi.";
            ViewData["RecentSessions"] = Workspace.Sessions;
            ViewData["SelectedSessionId"] = Workspace.SelectedSessionId;
            return Page();
        }

        try
        {
            var result = await _chatService.AskAsync(AskForm, accountId.Value);
            return RedirectToPage(new { documentId = result.DocumentId, sessionId = result.ChatSessionId });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage(new { documentId = AskForm.DocumentId, sessionId = AskForm.ChatSessionId });
        }
    }
}
