using AcademicDocumentRagSystem.Services.DTOs.Courses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.Services.Interfaces
{
    public interface ICourseService
    {
        Task<List<CourseDto>> GetAllAsync();

        Task<List<CourseDto>> SearchAsync(string? searchTerm);

        Task<CourseDto?> GetByIdAsync(int id);

        /// <summary>Courses currently assigned to the given teacher.</summary>
        Task<List<CourseSummaryDto>> GetByTeacherAsync(int teacherAccountId);

        /// <summary>Courses that have no teacher yet (for the assign flows).</summary>
        Task<List<CourseSummaryDto>> GetUnassignedAsync();

        Task CreateAsync(CreateCourseDto dto);

        Task UpdateAsync(UpdateCourseDto dto);

        Task DeleteAsync(int id);

        // ------------------------------------------------------------------
        // Teacher-course assignment. A course has at most one teacher; a
        // teacher may own many courses. All operations are idempotent and
        // never silently steal a course from another teacher — use
        // ReassignCourseAsync for an explicit transfer.
        // ------------------------------------------------------------------

        /// <summary>
        /// Assign several courses to a teacher in one atomic operation.
        /// Existing assignments of this teacher are kept. Fails (without
        /// saving anything) if any course belongs to another teacher.
        /// </summary>
        Task AssignCoursesToTeacherAsync(int teacherAccountId, IReadOnlyCollection<int> courseIds);

        Task AssignCourseToTeacherAsync(int teacherAccountId, int courseId);

        /// <summary>Remove the teacher from a course (documents/chats stay untouched).</summary>
        Task UnassignCourseAsync(int courseId);

        /// <summary>Explicitly transfer a course to a different teacher.</summary>
        Task ReassignCourseAsync(int courseId, int newTeacherAccountId);
    }
}
