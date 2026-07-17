using AcademicDocumentRagSystem.RazorPages.Hubs;
using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.DTOs.Accounts;
using AcademicDocumentRagSystem.Services.DTOs.Courses;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Accounts
{
    [SessionAuthorize("Admin")]
    public class IndexModel : PageModel
    {
        private readonly IAccountService _accountService;
        private readonly ICourseService _courseService;
        private readonly IHubContext<CourseHub> _courseHub;

        public IndexModel(
            IAccountService accountService,
            ICourseService courseService,
            IHubContext<CourseHub> courseHub)
        {
            _accountService = accountService;
            _courseService = courseService;
            _courseHub = courseHub;
        }

        public List<AccountListItemDto> Accounts { get; private set; } = new();

        /// <summary>Courses without a teacher — the only candidates for the
        /// "Gán thêm môn" flow (transferring an owned course is a separate,
        /// explicit action on the Courses page).</summary>
        public List<CourseSummaryDto> UnassignedCourses { get; private set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Role { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? Status { get; set; }

        [BindProperty]
        public UpdateAccountDto EditInput { get; set; } = new();

        public bool ShowEditModal { get; private set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public IActionResult OnPostCreateAsync()
        {
            return RedirectToPage("/Accounts/Create");
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            ModelState.Clear();

            if (!TryValidateModel(EditInput, nameof(EditInput)))
            {
                ShowEditModal = true;
                await LoadAsync();
                return Page();
            }

            try
            {
                await _accountService.UpdateAsync(EditInput);
                TempData["Success"] = $"Đã cập nhật tài khoản '{EditInput.Email}'.";
                return RedirectToPage(new { SearchTerm, Role, Status });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("EditInput.Email", ex.Message);
                ShowEditModal = true;
                await LoadAsync();
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                await _accountService.DeleteAsync(id);
                TempData["Success"] = "Đã xóa tài khoản.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { SearchTerm, Role, Status });
        }

        /// <summary>
        /// Assign one or more (currently unassigned) courses to a teacher.
        /// The teacher's existing courses are always kept.
        /// </summary>
        public async Task<IActionResult> OnPostAssignCoursesAsync(int teacherAccountId, List<int> courseIds)
        {
            if (courseIds == null || courseIds.Count == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất một môn học để gán.";
                return RedirectToPage(new { SearchTerm, Role, Status });
            }

            try
            {
                await _courseService.AssignCoursesToTeacherAsync(teacherAccountId, courseIds);

                var teacher = await _accountService.GetForEditAsync(teacherAccountId);
                var assigned = await _courseService.GetByTeacherAsync(teacherAccountId);
                foreach (var courseId in courseIds.Distinct())
                {
                    var course = assigned.FirstOrDefault(c => c.CourseId == courseId);
                    await _courseHub.Clients.All.SendAsync(CourseHub.CourseTeacherAssigned, new
                    {
                        courseId,
                        courseCode = course?.Code ?? string.Empty,
                        teacherAccountId,
                        teacherName = teacher?.FullName ?? string.Empty
                    });
                }
                await _courseHub.Clients.All.SendAsync(CourseHub.CoursesChanged);

                TempData["Success"] = $"Đã gán {courseIds.Distinct().Count()} môn học cho giảng viên.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { SearchTerm, Role, Status });
        }

        /// <summary>Remove one course from a teacher (documents/chats stay untouched).</summary>
        public async Task<IActionResult> OnPostUnassignCourseAsync(int courseId)
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

                TempData["Success"] = $"Đã bỏ giảng viên khỏi môn '{course?.Code}'.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToPage(new { SearchTerm, Role, Status });
        }

        private async Task LoadAsync()
        {
            Accounts = await _accountService.GetAllAsync(SearchTerm, Role, Status);
            UnassignedCourses = (await _courseService.GetUnassignedAsync())
                .Where(c => c.Status)
                .ToList();
        }
    }
}
