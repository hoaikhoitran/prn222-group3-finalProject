using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Accounts;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

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

    public List<SelectListItem> Courses { get; private set; } = new();

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
            case "Teacher accounts must be assigned to a course.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CourseId)}", "Giảng viên phải được gán một môn học.");
                break;
            case "Student accounts must not be assigned to a course.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CourseId)}", "Sinh viên không được gán môn học.");
                break;
            case "Assigned course was not found.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CourseId)}", "Môn học được chọn không tồn tại.");
                break;
            case "This course already has an assigned teacher.":
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.CourseId)}", "Môn học này đã có giảng viên được gán.");
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
        var courses = await _courseService.GetAllAsync();
        var teachers = await _accountService.GetAllAsync(null, CreateAccountDto.TeacherRole, null);
        var assignedCourseIds = teachers
            .Where(t => t.CourseId.HasValue)
            .Select(t => t.CourseId!.Value)
            .ToHashSet();

        Courses = courses
            .Where(c => c.Status && !assignedCourseIds.Contains(c.CourseId))
            .Select(c => new SelectListItem
            {
                Value = c.CourseId.ToString(),
                Text = $"{c.Code} - {c.Name}"
            })
            .ToList();
    }
}
