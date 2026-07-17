using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.Services.DTOs.Courses
{
    public class UpdateCourseDto
    {
        public int CourseId { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public bool Status { get; set; }

        /// <summary>Teacher responsible for this course; null = unassigned.</summary>
        public int? TeacherAccountId { get; set; }
    }
}
