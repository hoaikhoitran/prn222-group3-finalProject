using AcademicDocumentRagSystem.RazorPages.Hubs;
using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Accounts;
using AcademicDocumentRagSystem.Services.DTOs.Courses;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Courses;

[SessionAuthorize("Admin")]
public class CreateModel : PageModel
{
    private readonly ICourseService _courseService;
    private readonly IAccountService _accountService;
    private readonly IHubContext<CourseHub> _courseHub;

    public CreateModel(
        ICourseService courseService,
        IAccountService accountService,
        IHubContext<CourseHub> courseHub)
    {
        _courseService = courseService;
        _accountService = accountService;
        _courseHub = courseHub;
    }

    [BindProperty]
    public CreateCourseDto Input { get; set; } = new();

    /// <summary>
    /// Every ACTIVE teacher. Teachers already owning other courses stay
    /// selectable: one teacher may be responsible for many courses.
    /// </summary>
    public List<SelectListItem> Teachers { get; private set; } = new();

    public async Task OnGetAsync()
    {
        await LoadTeachersAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTeachersAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await _courseService.CreateAsync(Input);

            await _courseHub.Clients.All.SendAsync(
                CourseHub.CourseCreated, new { Input.Code, Input.Name });
            await _courseHub.Clients.All.SendAsync(CourseHub.CoursesChanged);

            TempData["Success"] = $"Đã tạo môn học '{Input.Code}'.";
            return RedirectToPage("/Courses/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    private async Task LoadTeachersAsync()
    {
        var teachers = await _accountService.GetAllAsync(null, CreateAccountDto.TeacherRole, true);

        Teachers = teachers
            .Select(t => new SelectListItem
            {
                Value = t.AccountId.ToString(),
                Text = $"{t.FullName} ({t.Email}) · {t.AssignedCourseCount} môn"
            })
            .ToList();
    }
}
