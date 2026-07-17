using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.Services.DTOs.Courses
{
    public class CourseDto
    {
        public int CourseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Status { get; set; }

        /// <summary>Teacher currently responsible for this course; null = unassigned.</summary>
        public int? TeacherAccountId { get; set; }
        public string? TeacherName { get; set; }
        public string? TeacherEmail { get; set; }

        public bool HasTeacher => TeacherAccountId.HasValue;
    }
}
