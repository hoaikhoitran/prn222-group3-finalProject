using AcademicDocumentRagSystem.RazorPages.Hubs;
using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Accounts;
using AcademicDocumentRagSystem.Services.DTOs.Courses;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Courses
{
    [SessionAuthorize("Admin")]
    public class IndexModel : PageModel
    {
        private readonly ICourseService _courseService;
        private readonly IAccountService _accountService;
        private readonly IHubContext<CourseHub> _courseHub;

        public IndexModel(
            ICourseService courseService,
            IAccountService accountService,
            IHubContext<CourseHub> courseHub)
        {
            _courseService = courseService;
            _accountService = accountService;
            _courseHub = courseHub;
        }

        public List<CourseDto> Courses { get; private set; } = new();

        /// <summary>
        /// Active teachers for the assign/transfer dropdowns. Teachers who
        /// already own courses stay selectable — one teacher may be
        /// responsible for many courses.
        /// </summary>
        public List<AccountListItemDto> ActiveTeachers { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public async Task OnGetAsync()
        {
            await LoadCoursesAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                await _courseService.DeleteAsync(id);

                await _courseHub.Clients.All.SendAsync(CourseHub.CourseDeleted, new { CourseId = id });
                await _courseHub.Clients.All.SendAsync(CourseHub.CoursesChanged);

                TempData["Success"] = "Đã xóa môn học.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { SearchTerm });
        }

        /// <summary>Assign a teacher to a course that has none yet.</summary>
        public async Task<IActionResult> OnPostAssignTeacherAsync(int courseId, int teacherAccountId)
        {
            try
            {
                await _courseService.AssignCourseToTeacherAsync(teacherAccountId, courseId);

                var course = await _courseService.GetByIdAsync(courseId);
                await _courseHub.Clients.All.SendAsync(CourseHub.CourseTeacherAssigned, new
                {
                    courseId,
                    courseCode = course?.Code ?? string.Empty,
                    teacherAccountId,
                    teacherName = course?.TeacherName ?? string.Empty
                });
                await _courseHub.Clients.All.SendAsync(CourseHub.CoursesChanged);

                TempData["Success"] = $"Đã gán giảng viên phụ trách môn '{course?.Code}'.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { SearchTerm });
        }

        /// <summary>Explicitly transfer a course to another teacher.</summary>
        public async Task<IActionResult> OnPostReassignTeacherAsync(int courseId, int teacherAccountId)
        {
            try
            {
                await _courseService.ReassignCourseAsync(courseId, teacherAccountId);

                var course = await _courseService.GetByIdAsync(courseId);
                await _courseHub.Clients.All.SendAsync(CourseHub.CourseTeacherChanged, new
                {
                    courseId,
                    courseCode = course?.Code ?? string.Empty,
                    teacherAccountId,
                    teacherName = course?.TeacherName ?? string.Empty
                });
                await _courseHub.Clients.All.SendAsync(CourseHub.CoursesChanged);

                TempData["Success"] = $"Đã chuyển môn '{course?.Code}' sang giảng viên mới.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { SearchTerm });
        }

        /// <summary>Remove the teacher from a course. Documents/chats are kept.</summary>
        public async Task<IActionResult> OnPostUnassignTeacherAsync(int courseId)
        {
            try
            {
                var course = await _courseService.GetByIdAsync(courseId);

                await _courseService.UnassignCourseAsync(courseId);

                await _courseHub.Clients.All.SendAsync(CourseHub.CourseTeacherUnassigned, new
                {
                    courseId,
                    courseCode = course?.Code ?? string.Empty
                });
                await _courseHub.Clients.All.SendAsync(CourseHub.CoursesChanged);

                TempData["Success"] = $"Đã bỏ gán giảng viên khỏi môn '{course?.Code}'.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { SearchTerm });
        }

        private async Task LoadCoursesAsync()
        {
            Courses = await _courseService.SearchAsync(SearchTerm);
            ActiveTeachers = await _accountService.GetAllAsync(null, CreateAccountDto.TeacherRole, true);
        }
    }
}
