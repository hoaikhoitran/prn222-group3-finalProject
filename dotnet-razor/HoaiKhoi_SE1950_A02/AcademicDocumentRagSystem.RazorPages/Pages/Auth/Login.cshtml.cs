using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Auth;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Auth
{
    public class LoginModel : PageModel
    {
        private readonly IAccountService _accountService;

        public LoginModel(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [BindProperty]
        public LoginDto Input { get; set; } = new();

        public IActionResult OnGet()
        {
            // Already signed in? Send them straight to their dashboard.
            var existing = HttpContext.Session.GetString(SessionKeys.RoleName);
            if (!string.IsNullOrEmpty(existing))
            {
                return RedirectToDashboard(existing);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await _accountService.LoginAsync(Input);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Login failed.");
                return Page();
            }

            HttpContext.Session.SetString(SessionKeys.Email, result.Email);
            HttpContext.Session.SetString(SessionKeys.FullName, result.FullName);
            HttpContext.Session.SetString(SessionKeys.RoleName, result.RoleName);

            if (result.AccountId.HasValue)
            {
                HttpContext.Session.SetInt32(SessionKeys.AccountId, result.AccountId.Value);
            }

            // No course data goes into the session: a teacher can own many
            // courses and assignments can change mid-session, so every page
            // loads the current assignment list from the database instead.
            return RedirectToDashboard(result.RoleName);
        }

        private IActionResult RedirectToDashboard(string roleName) => roleName switch
        {
            "Admin" => RedirectToPage("/Admin/Index"),
            "Teacher" => RedirectToPage("/Teacher/Courses"),
            "Student" => RedirectToPage("/Student/Chat"),
            _ => RedirectToPage("/Auth/Login")
        };
    }
}
