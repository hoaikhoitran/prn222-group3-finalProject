using AcademicDocumentRagSystem.Services.DTOs.Courses;

namespace AcademicDocumentRagSystem.Services.DTOs.Accounts;

public class AccountListItemDto
{
    public int AccountId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int Role { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public bool Status { get; set; }

    /// <summary>Number of courses this teacher is responsible for (0 for students).</summary>
    public int AssignedCourseCount => AssignedCourses.Count;

    /// <summary>Courses assigned to this teacher, ordered by code.</summary>
    public List<CourseSummaryDto> AssignedCourses { get; set; } = new();
}
