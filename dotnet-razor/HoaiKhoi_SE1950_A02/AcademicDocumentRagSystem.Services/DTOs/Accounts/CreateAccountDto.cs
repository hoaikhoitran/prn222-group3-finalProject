using System.ComponentModel.DataAnnotations;

namespace AcademicDocumentRagSystem.Services.DTOs.Accounts;

public class CreateAccountDto : IValidatableObject
{
    public const int StudentRole = 1;
    public const int TeacherRole = 2;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất {2} ký tự.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá {1} ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
    [Range(1, 2, ErrorMessage = "Vai trò phải là Student hoặc Teacher.")]
    [Display(Name = "Vai trò")]
    public int Role { get; set; } = StudentRole;

    /// <summary>
    /// Courses to assign when creating a Teacher. Optional: a teacher may be
    /// created without any course and assigned later. Must stay empty for
    /// Students — students are never assigned to courses.
    /// </summary>
    [Display(Name = "Môn học phụ trách")]
    public List<int> CourseIds { get; set; } = new();

    [Display(Name = "Tài khoản hoạt động")]
    public bool Status { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Role == StudentRole && CourseIds.Count > 0)
        {
            yield return new ValidationResult(
                "Sinh viên không được gán môn học.",
                [nameof(CourseIds)]);
        }
    }
}
