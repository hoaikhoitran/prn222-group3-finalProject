using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces;
using AcademicDocumentRagSystem.Services.DTOs.Courses;
using AcademicDocumentRagSystem.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.Services.Implementations
{
    public class CourseService : ICourseService
    {
        private const int TeacherRole = 2;

        private readonly ICourseRepository _courseRepository;
        private readonly IAccountRepository _accountRepository;

        public CourseService(ICourseRepository courseRepository, IAccountRepository accountRepository)
        {
            _courseRepository = courseRepository;
            _accountRepository = accountRepository;
        }

        public async Task CreateAsync(CreateCourseDto dto)
        {
            var existingCourse = await _courseRepository.GetByCodeAsync(dto.Code);
            if (existingCourse != null)
            {
                throw new ArgumentException("Mã môn học này đã tồn tại.");
            }

            if (dto.TeacherAccountId.HasValue)
            {
                await EnsureValidTeacherAsync(dto.TeacherAccountId.Value);
            }

            var course = new Course
            {
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                Status = dto.Status,
                TeacherAccountId = dto.TeacherAccountId,
                CreatedAt = DateTime.UtcNow
            };
            await _courseRepository.AddAsync(course);
            await _courseRepository.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            if (course == null)
            {
                throw new ArgumentException("Không tìm thấy môn học.");
            }
            _courseRepository.Delete(course);
            await _courseRepository.SaveChangesAsync();
        }

        public async Task UpdateAsync(UpdateCourseDto dto)
        {
            var course = await _courseRepository.GetByIdAsync(dto.CourseId);
            if (course == null)
            {
                throw new ArgumentException("Không tìm thấy môn học.");
            }
            var existingCourse = await _courseRepository.GetByCodeAsync(dto.Code);
            if (existingCourse != null && existingCourse.CourseId != dto.CourseId)
            {
                throw new ArgumentException("Mã môn học này đã được dùng bởi môn khác.");
            }

            if (dto.TeacherAccountId.HasValue && dto.TeacherAccountId != course.TeacherAccountId)
            {
                await EnsureValidTeacherAsync(dto.TeacherAccountId.Value);
            }

            course.Code = dto.Code;
            course.Name = dto.Name;
            course.Description = dto.Description;
            course.Status = dto.Status;
            course.TeacherAccountId = dto.TeacherAccountId;
            course.UpdatedAt = DateTime.UtcNow;
            _courseRepository.Update(course);
            await _courseRepository.SaveChangesAsync();
        }

        public async Task<List<CourseDto>> GetAllAsync()
        {
            var courses = await _courseRepository.GetAllAsync();
            return courses.Select(MapToDto).ToList();
        }

        public async Task<List<CourseDto>> SearchAsync(string? searchTerm)
        {
            var courses = await _courseRepository.SearchAsync(searchTerm);
            return courses.Select(MapToDto).ToList();
        }

        public async Task<CourseDto?> GetByIdAsync(int id)
        {
            var course = await _courseRepository.GetByIdAsync(id);
            return course == null ? null : MapToDto(course);
        }

        public async Task<List<CourseSummaryDto>> GetByTeacherAsync(int teacherAccountId)
        {
            var courses = await _courseRepository.GetByTeacherAsync(teacherAccountId);
            return courses.Select(MapToSummary).ToList();
        }

        public async Task<List<CourseSummaryDto>> GetUnassignedAsync()
        {
            var courses = await _courseRepository.GetUnassignedAsync();
            return courses.Select(MapToSummary).ToList();
        }

        // ------------------------------------------------------------------
        // Assignment
        // ------------------------------------------------------------------

        public async Task AssignCoursesToTeacherAsync(int teacherAccountId, IReadOnlyCollection<int> courseIds)
        {
            var teacher = await EnsureValidTeacherAsync(teacherAccountId);

            var distinctIds = courseIds.Distinct().ToList();
            var coursesToAssign = new List<Course>();

            // Validate EVERYTHING before mutating anything so a bad course id
            // can never leave a half-saved batch behind.
            foreach (var courseId in distinctIds)
            {
                var course = await _courseRepository.GetByIdAsync(courseId);

                if (course == null)
                {
                    throw new ArgumentException($"Môn học #{courseId} không tồn tại.");
                }

                if (course.TeacherAccountId == teacherAccountId)
                {
                    continue; // already assigned to this teacher — idempotent, skip
                }

                if (course.TeacherAccountId.HasValue)
                {
                    throw new ArgumentException(
                        $"Môn {course.Code} đang do giảng viên khác phụ trách. "
                        + "Hãy dùng chức năng \"Chuyển giảng viên\" nếu muốn chuyển môn này.");
                }

                coursesToAssign.Add(course);
            }

            foreach (var course in coursesToAssign)
            {
                course.TeacherAccountId = teacher.AccountId;
                course.UpdatedAt = DateTime.UtcNow;
                _courseRepository.Update(course);
            }

            // Single SaveChanges = one database transaction for the whole batch.
            await _courseRepository.SaveChangesAsync();
        }

        public Task AssignCourseToTeacherAsync(int teacherAccountId, int courseId)
            => AssignCoursesToTeacherAsync(teacherAccountId, new[] { courseId });

        public async Task UnassignCourseAsync(int courseId)
        {
            var course = await _courseRepository.GetByIdAsync(courseId);

            if (course == null)
            {
                throw new ArgumentException("Không tìm thấy môn học.");
            }

            if (course.TeacherAccountId == null)
            {
                return; // already unassigned — idempotent
            }

            course.TeacherAccountId = null;
            course.TeacherAccount = null;
            course.UpdatedAt = DateTime.UtcNow;
            _courseRepository.Update(course);
            await _courseRepository.SaveChangesAsync();
        }

        public async Task ReassignCourseAsync(int courseId, int newTeacherAccountId)
        {
            var course = await _courseRepository.GetByIdAsync(courseId);

            if (course == null)
            {
                throw new ArgumentException("Không tìm thấy môn học.");
            }

            var teacher = await EnsureValidTeacherAsync(newTeacherAccountId);

            if (course.TeacherAccountId == newTeacherAccountId)
            {
                return; // already owned by this teacher — idempotent
            }

            // Explicit transfer: the old assignment (if any) is replaced.
            // Documents, chat sessions and chunks are untouched; only the
            // management permission moves from now on.
            course.TeacherAccountId = teacher.AccountId;
            course.TeacherAccount = null;
            course.UpdatedAt = DateTime.UtcNow;
            _courseRepository.Update(course);
            await _courseRepository.SaveChangesAsync();
        }

        private async Task<Account> EnsureValidTeacherAsync(int teacherAccountId)
        {
            var teacher = await _accountRepository.GetByIdAsync(teacherAccountId);

            if (teacher == null)
            {
                throw new ArgumentException("Không tìm thấy tài khoản giảng viên.");
            }

            if (teacher.Role != TeacherRole)
            {
                throw new ArgumentException("Chỉ có thể gán môn học cho tài khoản giảng viên.");
            }

            if (!teacher.Status)
            {
                throw new ArgumentException("Giảng viên này đang bị khóa nên không thể nhận môn học mới.");
            }

            return teacher;
        }

        private static CourseDto MapToDto(Course c) => new CourseDto
        {
            CourseId = c.CourseId,
            Code = c.Code,
            Name = c.Name,
            Description = c.Description,
            Status = c.Status,
            TeacherAccountId = c.TeacherAccountId,
            TeacherName = c.TeacherAccount?.FullName,
            TeacherEmail = c.TeacherAccount?.Email
        };

        private static CourseSummaryDto MapToSummary(Course c) => new CourseSummaryDto
        {
            CourseId = c.CourseId,
            Code = c.Code,
            Name = c.Name,
            Status = c.Status
        };
    }
}
