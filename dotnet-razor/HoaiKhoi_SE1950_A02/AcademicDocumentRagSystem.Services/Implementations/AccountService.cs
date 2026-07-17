using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces;
using AcademicDocumentRagSystem.Services.DTOs.Accounts;
using AcademicDocumentRagSystem.Services.DTOs.Auth;
using AcademicDocumentRagSystem.Services.DTOs.Courses;
using AcademicDocumentRagSystem.Services.Email;
using AcademicDocumentRagSystem.Services.Email.Models;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AcademicDocumentRagSystem.Services.Implementations
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateRenderer _emailTemplateRenderer;
        private readonly ILogger<AccountService> _logger;

        public AccountService(
            IAccountRepository accountRepository,
            ICourseRepository courseRepository,
            IConfiguration configuration,
            IEmailService emailService,
            IEmailTemplateRenderer emailTemplateRenderer,
            ILogger<AccountService> logger)
        {
            _accountRepository = accountRepository;
            _courseRepository = courseRepository;
            _configuration = configuration;
            _emailService = emailService;
            _emailTemplateRenderer = emailTemplateRenderer;
            _logger = logger;
        }

        public async Task<LoginResultDto> LoginAsync(LoginDto dto)
        {
            var adminEmail = _configuration["AdminAccount:Email"];
            var adminPassword = _configuration["AdminAccount:Password"];

            if (dto.Email == adminEmail && dto.Password == adminPassword)
            {
                return new LoginResultDto
                {
                    IsSuccess = true,
                    AccountId = null,
                    Email = dto.Email,
                    FullName = "System Admin",
                    RoleName = "Admin",
                    Role = null
                };
            }

            var account = await _accountRepository.GetByEmailAndPasswordAsync(dto.Email, dto.Password);

            if (account == null)
            {
                return new LoginResultDto
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid email or password."
                };
            }

            return new LoginResultDto
            {
                IsSuccess = true,
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Role = account.Role,
                RoleName = GetRoleName(account.Role),
                // Informational only: permissions are re-checked per request
                // against Courses.TeacherAccountId, never against the session.
                AssignedCourses = MapAssignedCourses(account)
            };
        }

        public async Task<List<AccountListItemDto>> GetAllAsync(string? searchTerm, int? role, bool? status)
        {
            var accounts = await _accountRepository.GetAllAsync(searchTerm, role, status);

            return accounts.Select(a => new AccountListItemDto
            {
                AccountId = a.AccountId,
                Email = a.Email,
                FullName = a.FullName,
                Role = a.Role,
                RoleName = GetRoleName(a.Role),
                Status = a.Status,
                AssignedCourses = MapAssignedCourses(a)
            }).ToList();
        }

        public async Task<UpdateAccountDto?> GetForEditAsync(int id)
        {
            var account = await _accountRepository.GetByIdAsync(id);

            if (account == null)
            {
                return null;
            }

            return new UpdateAccountDto
            {
                AccountId = account.AccountId,
                Email = account.Email,
                FullName = account.FullName,
                Role = account.Role,
                Status = account.Status
            };
        }

        public async Task<CreateAccountResult> CreateAsync(CreateAccountDto dto)
        {
            await ValidateAccountAsync(dto.Email, dto.Role, null);

            var courseIds = dto.CourseIds.Distinct().ToList();

            if (dto.Role == CreateAccountDto.StudentRole && courseIds.Count > 0)
            {
                throw new ArgumentException("Student accounts must not be assigned to a course.");
            }

            // Validate every requested course BEFORE creating anything so a bad
            // selection can never leave a half-created assignment batch.
            var coursesToAssign = new List<Course>();
            foreach (var courseId in courseIds)
            {
                var course = await _courseRepository.GetByIdAsync(courseId);

                if (course == null)
                {
                    throw new ArgumentException("Assigned course was not found.");
                }

                if (course.TeacherAccountId.HasValue)
                {
                    throw new ArgumentException(
                        $"Môn {course.Code} đang do giảng viên khác phụ trách. "
                        + "Hãy dùng chức năng \"Chuyển giảng viên\" nếu muốn chuyển môn này.");
                }

                coursesToAssign.Add(course);
            }

            var account = new Account
            {
                Email = dto.Email.Trim(),
                Password = dto.Password,
                FullName = dto.FullName.Trim(),
                Role = dto.Role,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow
            };

            // Wire the assignments through the navigation property so EF saves
            // the new account AND its course assignments in ONE transaction.
            foreach (var course in coursesToAssign)
            {
                course.TeacherAccount = account;
                course.UpdatedAt = DateTime.UtcNow;
                _courseRepository.Update(course);
            }

            await _accountRepository.AddAsync(account);
            await _accountRepository.SaveChangesAsync();

            var result = new CreateAccountResult { AccountId = account.AccountId };

            // Onboarding email is only sent for Teacher/Lecturer accounts. Email
            // delivery must never corrupt or roll back the already-persisted account,
            // so failures are caught, logged, and surfaced via the result instead.
            if (account.Role == 2)
            {
                result.EmailAttempted = true;

                try
                {
                    await SendAccountCreatedEmailAsync(account, dto.Password, coursesToAssign);
                    result.EmailSent = true;
                }
                catch (Exception ex)
                {
                    result.EmailSent = false;
                    result.EmailError = ex.Message;
                    _logger.LogError(
                        ex,
                        "Lecturer account {Email} (id {AccountId}) was created but the onboarding email failed to send.",
                        account.Email,
                        account.AccountId);
                }
            }

            return result;
        }

        private async Task SendAccountCreatedEmailAsync(
            Account account, string temporaryPassword, List<Course> assignedCourses)
        {
            // The welcome template has one course slot; with 1-N assignment we
            // show the full list, or the explicit "not assigned yet" message.
            var courseName = assignedCourses.Count == 0
                ? "Chưa được phân công môn học"
                : string.Join(", ", assignedCourses
                    .OrderBy(c => c.Code)
                    .Select(c => $"{c.Code} - {c.Name}"));

            var model = new TeacherWelcomeEmailModel
            {
                TeacherName = account.FullName,
                Email = account.Email,
                Password = temporaryPassword,
                CourseName = courseName,
                LoginUrl = _configuration["App:LoginUrl"] ?? string.Empty,
                CurrentYear = DateTime.UtcNow.Year
            };

            // The service composes the data model and renders the premium HTML
            // template; IEmailService only transports the resulting message.
            var htmlBody = _emailTemplateRenderer.RenderTeacherWelcome(model);

            await _emailService.SendEmailAsync(
                account.Email,
                "Welcome to Academic Document RAG System",
                htmlBody);
        }

        public async Task UpdateAsync(UpdateAccountDto dto)
        {
            var account = await _accountRepository.GetByIdAsync(dto.AccountId);

            if (account == null)
            {
                throw new ArgumentException("Account not found.");
            }

            await ValidateAccountAsync(dto.Email, dto.Role, dto.AccountId);

            // Demoting a teacher who still owns courses would leave those
            // courses managed by a student — block it with a clear message.
            if (dto.Role != 2 && account.Role == 2)
            {
                var stillAssigned = await _courseRepository.GetByTeacherAsync(account.AccountId);
                if (stillAssigned.Count > 0)
                {
                    throw new ArgumentException(
                        "Giảng viên này còn phụ trách "
                        + string.Join(", ", stillAssigned.Select(c => c.Code))
                        + ". Hãy bỏ gán hoặc chuyển các môn trước khi đổi vai trò.");
                }
            }

            account.Email = dto.Email.Trim();
            account.FullName = dto.FullName.Trim();
            account.Role = dto.Role;
            account.Status = dto.Status;
            account.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                account.Password = dto.Password;
            }

            _accountRepository.Update(account);
            await _accountRepository.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var account = await _accountRepository.GetByIdAsync(id);

            if (account == null)
            {
                throw new ArgumentException("Account not found.");
            }

            _accountRepository.Delete(account);
            await _accountRepository.SaveChangesAsync();
        }

        private async Task ValidateAccountAsync(string email, int role, int? accountId)
        {
            if (role != 1 && role != 2)
            {
                throw new ArgumentException("Role must be Student or Teacher.");
            }

            var normalizedEmail = email.Trim();
            var adminEmail = _configuration["AdminAccount:Email"];

            if (!string.IsNullOrWhiteSpace(adminEmail) &&
                string.Equals(normalizedEmail, adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("This email is reserved for the system admin account.");
            }

            var existingAccount = accountId.HasValue
                ? await _accountRepository.GetByEmailExceptIdAsync(normalizedEmail, accountId.Value)
                : await _accountRepository.GetByEmailAsync(normalizedEmail);

            if (existingAccount != null)
            {
                throw new ArgumentException("Email is already used by another account.");
            }
        }

        private static List<CourseSummaryDto> MapAssignedCourses(Account account)
        {
            return account.TeachingCourses
                .OrderBy(c => c.Code)
                .Select(c => new CourseSummaryDto
                {
                    CourseId = c.CourseId,
                    Code = c.Code,
                    Name = c.Name,
                    Status = c.Status
                })
                .ToList();
        }

        private static string GetRoleName(int role)
        {
            return role == 1 ? "Student" : "Teacher";
        }
    }
}
