using AcademicDocumentRagSystem.DataAccess.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces
{
    public interface ICourseRepository
    {
        Task<List<Course>> GetAllAsync();

        Task<List<Course>> SearchAsync(string? searchTerm);

        Task<Course?> GetByIdAsync(int id);

        Task<Course?> GetByCodeAsync(string code);

        /// <summary>Courses currently assigned to the given teacher.</summary>
        Task<List<Course>> GetByTeacherAsync(int teacherAccountId);

        /// <summary>Courses that have no teacher yet.</summary>
        Task<List<Course>> GetUnassignedAsync();

        /// <summary>
        /// True when the course is currently assigned to the given teacher.
        /// This is the single permission source for teacher-course checks —
        /// never trust a CourseId coming from a form without going through it.
        /// </summary>
        Task<bool> IsAssignedToTeacherAsync(int courseId, int teacherAccountId);

        Task AddAsync(Course course);

        void Update(Course course);

        void Delete(Course course);

        Task SaveChangesAsync();
    }
}
