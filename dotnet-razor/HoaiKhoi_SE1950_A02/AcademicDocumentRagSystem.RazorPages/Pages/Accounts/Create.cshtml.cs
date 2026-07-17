using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Accounts;
using AcademicDocumentRagSystem.Services.DTOs.Courses;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Accounts;

[SessionAuthorize("Admin")]
public class CreateModel : PageModel
{
    private readonly IAccountService _accountService;
    private readonly ICourseService _courseService;

    public CreateModel(IAccountService accountService, ICourseService courseService)
    {
        _accountService = accountService;
        _courseService = courseService;
    }

    [BindProperty]
    public CreateAccountDto Input { get; set; } = new();

    /// <summary>
    /// Active courses that have no teacher yet. Creating a teacher may assign
    /// several of them at once; courses owned by another teacher must go
    /// through the explicit "Chuyển giảng viên" flow instead.
    /// </summary>
    public List<CourseSummaryDto> UnassignedCourses { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadCoursesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCoursesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var result = await _accountService.CreateAsync(Input);

            if (result.EmailAttempted && !result.EmailSent)
            {
                TempData["Warning"] =
                    $"Tài khoản '{Input.Email}' đã được tạo, nhưng không gửi được email thông báo. " +
                    $"Lý do: {result.EmailError}";
            }
            else if (result.EmailSent)
            {
                TempData["Success"] =
                    $"Tài khoản giảng viên '{Input.Email}' đã được tạo và email thông tin đăng nhập đã được gửi.";
            }
            else
            {
                TempData["Success"] = $"Tài khoản '{Input.Email}' đã được tạo.";
            }

            return RedirectToPage("/Accounts/Index");
        }
        catch (ArgumentException ex)
        {
            MapServiceException(ex);
            return Page();
        }
    }

    private void MapServiceException(ArgumentException ex)
    {
        switch (ex.Message)
        {
            case "Email is already used by another account.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Email)}", "Email đã được sử dụng bởi tài khoản khác.");
                break;
            case "This email is reserved for the system admin account.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Email)}", "Email này được dành riêng cho tài khoản quản trị hệ thống.");
                break;
            case "Student accounts must not be assigned to a course.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CourseIds)}", "Sinh viên không được gán môn học.");
                break;
            case "Assigned course was not found.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CourseIds)}", "Một trong các môn học được chọn không tồn tại.");
                break;
            case "Role must be Student or Teacher.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Role)}", "Vai trò phải là Student hoặc Teacher.");
                break;
            default:
                ModelState.AddModelError(string.Empty, ex.Message);
                break;
        }
    }

    private async Task LoadCoursesAsync()
    {
        UnassignedCourses = (await _courseService.GetUnassignedAsync())
            .Where(c => c.Status)
            .ToList();
    }
}
