using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.DataAccess.Repositories.Implementations
{
    public class CourseRepository : ICourseRepository
    {
        private readonly AcademicRagDbContext _context;
        public CourseRepository(AcademicRagDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(Course course)
        {
            await _context.Courses.AddAsync(course);
        }

        public void Delete(Course course)
        {
            _context.Courses.Remove(course);
        }

        public async Task<List<Course>> GetAllAsync()
        {
            return await _context.Courses
                .Include(c => c.TeacherAccount)
                .ToListAsync();
        }

        public async Task<List<Course>> SearchAsync(string? searchTerm)
        {
            var query = _context.Courses
                .Include(c => c.TeacherAccount)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var keyword = searchTerm.Trim();
                query = query.Where(c => c.Code.Contains(keyword) || c.Name.Contains(keyword));
            }

            return await query
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<Course?> GetByCodeAsync(string code)
        {
            return await _context.Courses
                .Include(c => c.TeacherAccount)
                .FirstOrDefaultAsync(c => c.Code == code);
        }

        public async Task<Course?> GetByIdAsync(int id)
        {
            return await _context.Courses
                .Include(c => c.TeacherAccount)
                .FirstOrDefaultAsync(c => c.CourseId == id);
        }

        public async Task<List<Course>> GetByTeacherAsync(int teacherAccountId)
        {
            return await _context.Courses
                .Where(c => c.TeacherAccountId == teacherAccountId)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<List<Course>> GetUnassignedAsync()
        {
            return await _context.Courses
                .Where(c => c.TeacherAccountId == null)
                .OrderBy(c => c.Code)
                .ToListAsync();
        }

        public async Task<bool> IsAssignedToTeacherAsync(int courseId, int teacherAccountId)
        {
            return await _context.Courses.AnyAsync(c =>
                c.CourseId == courseId && c.TeacherAccountId == teacherAccountId);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Update(Course course)
        {
            _context.Courses.Update(course);
        }
    }
}
