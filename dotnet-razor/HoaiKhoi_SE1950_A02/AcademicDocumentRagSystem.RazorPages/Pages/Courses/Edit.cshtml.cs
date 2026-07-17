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
public class EditModel : PageModel
{
    private readonly ICourseService _courseService;
    private readonly IAccountService _accountService;
    private readonly IHubContext<CourseHub> _courseHub;

    public EditModel(
        ICourseService courseService,
        IAccountService accountService,
        IHubContext<CourseHub> courseHub)
    {
        _courseService = courseService;
        _accountService = accountService;
        _courseHub = courseHub;
    }

    [BindProperty]
    public UpdateCourseDto Input { get; set; } = new();

    /// <summary>Every ACTIVE teacher (multi-course ownership allowed).</summary>
    public List<SelectListItem> Teachers { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var course = await _courseService.GetByIdAsync(id);

        if (course == null)
        {
            TempData["Error"] = "Không tìm thấy môn học.";
            return RedirectToPage("/Courses/Index");
        }

        Input = new UpdateCourseDto
        {
            CourseId = course.CourseId,
            Code = course.Code,
            Name = course.Name,
            Description = course.Description,
            Status = course.Status,
            TeacherAccountId = course.TeacherAccountId
        };

        await LoadTeachersAsync();
        return Page();
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
            var before = await _courseService.GetByIdAsync(Input.CourseId);
            var previousTeacherId = before?.TeacherAccountId;

            await _courseService.UpdateAsync(Input);

            await _courseHub.Clients.All.SendAsync(
                CourseHub.CourseUpdated, new { Input.CourseId, Input.Code, Input.Name });

            // Emit the specific assignment event when the teacher changed here.
            if (previousTeacherId != Input.TeacherAccountId)
            {
                var after = await _courseService.GetByIdAsync(Input.CourseId);
                var eventName = Input.TeacherAccountId == null
                    ? CourseHub.CourseTeacherUnassigned
                    : previousTeacherId == null
                        ? CourseHub.CourseTeacherAssigned
                        : CourseHub.CourseTeacherChanged;

                await _courseHub.Clients.All.SendAsync(eventName, new
                {
                    courseId = Input.CourseId,
                    courseCode = Input.Code,
                    teacherAccountId = Input.TeacherAccountId,
                    teacherName = after?.TeacherName ?? string.Empty
                });
            }

            await _courseHub.Clients.All.SendAsync(CourseHub.CoursesChanged);

            TempData["Success"] = $"Đã cập nhật môn học '{Input.Code}'.";
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
