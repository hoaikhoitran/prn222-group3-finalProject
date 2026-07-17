using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcademicDocumentRagSystem.Services.DTOs.Courses;

namespace AcademicDocumentRagSystem.Services.DTOs.Auth
{
    public class LoginResultDto
    {
        public bool IsSuccess { get; set; }

        public string? ErrorMessage { get; set; }

        public int? AccountId { get; set; }

        public string Email { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string RoleName { get; set; } = string.Empty;

        public int? Role { get; set; }

        /// <summary>
        /// Courses a teacher is responsible for at login time. Informational
        /// only — permissions are always re-checked against the database, and
        /// nothing course-related is stored in the session (a teacher can own
        /// many courses and assignments change while they are signed in).
        /// </summary>
        public List<CourseSummaryDto> AssignedCourses { get; set; } = new();
    }
}
