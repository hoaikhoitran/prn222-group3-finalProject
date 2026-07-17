namespace AcademicDocumentRagSystem.Services.DTOs.Courses
{
    /// <summary>Compact course info for lists such as a teacher's assigned courses.</summary>
    public class CourseSummaryDto
    {
        public int CourseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Status { get; set; }
    }
}
