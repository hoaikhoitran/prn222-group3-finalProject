using AcademicDocumentRagSystem.Services.DTOs.Courses;

namespace AcademicDocumentRagSystem.RazorPages.ViewModels;

public class TeacherCourseCardViewModel
{
    public CourseDto Course { get; set; } = new();

    public int DocumentCount { get; set; }

    public int IndexedCount { get; set; }

    public int TotalChunks { get; set; }
}
