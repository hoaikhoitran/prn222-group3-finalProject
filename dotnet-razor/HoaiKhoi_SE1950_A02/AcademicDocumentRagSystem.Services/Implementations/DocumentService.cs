using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces;
using AcademicDocumentRagSystem.Services.Chunking;
using AcademicDocumentRagSystem.Services.DTOs.Courses;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.DTOs.Rag;
using AcademicDocumentRagSystem.Services.Interfaces;
using AcademicDocumentRagSystem.Services.RagIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.Services.Implementations
{
    public class DocumentService : IDocumentService
    {
        private const int TeacherRole = 2;
        private const int RagIndexMaxChunks = 10000;

        private const string WrongCourseMessage =
            "Bạn không có quyền upload tài liệu cho môn học này.";

        private const string DuplicateFileMessage =
            "This document file has already been uploaded for this course.";

        private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".pptx", ".txt" };

        private readonly IDocumentRepository _documentRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IDocumentChunkRepository _chunkRepository;
        private readonly IDocumentChunkConfigRepository _chunkConfigRepository;
        private readonly IDocumentIndexLogRepository _indexLogRepository;
        private readonly IChunkPreviewGenerator _chunkPreviewGenerator;
        private readonly IRagClient _ragClient;
        private readonly IConfiguration _configuration;

        public DocumentService(
            IDocumentRepository documentRepository,
            IAccountRepository accountRepository,
            ICourseRepository courseRepository,
            IDocumentChunkRepository chunkRepository,
            IDocumentChunkConfigRepository chunkConfigRepository,
            IDocumentIndexLogRepository indexLogRepository,
            IChunkPreviewGenerator chunkPreviewGenerator,
            IRagClient ragClient,
            IConfiguration configuration)
        {
            _documentRepository = documentRepository;
            _accountRepository = accountRepository;
            _courseRepository = courseRepository;
            _chunkRepository = chunkRepository;
            _chunkConfigRepository = chunkConfigRepository;
            _indexLogRepository = indexLogRepository;
            _chunkPreviewGenerator = chunkPreviewGenerator;
            _ragClient = ragClient;
            _configuration = configuration;
        }

        public async Task<List<DocumentListItemDto>> GetByTeacherAsync(int accountId)
        {
            var documents = await _documentRepository.GetBySubmitterAsync(accountId);
            return await MapListItemsAsync(documents);
        }

        public async Task<List<DocumentListItemDto>> GetAllForAdminAsync(DocumentFilterDto filter)
        {
            var documents = await _documentRepository.GetForAdminAsync(
                filter.CourseId, filter.CourseCode, filter.UploadStatus, filter.IndexStatus);

            return await MapListItemsAsync(documents);
        }

        public async Task<List<CourseDto>> GetCourseFilterOptionsAsync()
        {
            var courses = await _courseRepository.GetAllAsync();

            return courses
                .OrderBy(c => c.Code)
                .Select(c => new CourseDto
                {
                    CourseId = c.CourseId,
                    Code = c.Code,
                    Name = c.Name,
                    Description = c.Description,
                    Status = c.Status
                })
                .ToList();
        }

        private async Task<List<DocumentListItemDto>> MapListItemsAsync(List<Document> documents)
        {
            var idsWithChunks = await _chunkRepository.GetDocumentIdsWithChunksAsync(
                documents.Select(d => d.DocumentId).ToList());

            return documents.Select(d => new DocumentListItemDto
            {
                DocumentId = d.DocumentId,
                Title = d.Title,
                Description = d.Description,
                CourseCode = d.CourseCode,
                CourseName = d.Course?.Name,
                Chapter = d.Chapter,
                OriginalFileName = d.OriginalFileName,
                FileType = d.FileType,
                FileSize = d.FileSize,
                UploadStatus = d.UploadStatus,
                IndexStatus = d.IndexStatus,
                TotalChunks = d.TotalChunks,
                IndexError = d.IndexError,
                SubmittedByAccountId = d.SubmittedByAccountId,
                SubmittedByFullName = d.SubmittedByAccount?.FullName,
                SubmittedByEmail = d.SubmittedByEmail,
                CreatedAt = d.CreatedAt,
                IndexedAt = d.IndexedAt,
                HasChunks = idsWithChunks.Contains(d.DocumentId)
            }).ToList();
        }

        public async Task<List<CourseDto>> GetUploadCoursesForTeacherAsync(int accountId)
        {
            var account = await _accountRepository.GetByIdAsync(accountId);

            if (account == null || account.Role != TeacherRole || account.CourseId == null)
            {
                return new List<CourseDto>();
            }

            var course = await _courseRepository.GetByIdAsync(account.CourseId.Value);

            if (course == null || !course.Status)
            {
                return new List<CourseDto>();
            }

            return new List<CourseDto>
            {
                new()
                {
                    CourseId = course.CourseId,
                    Code = course.Code,
                    Name = course.Name,
                    Description = course.Description,
                    Status = course.Status
                }
            };
        }

        public async Task<int> UploadAndIndexAsync(DocumentUploadDto dto, int accountId, string email)
        {
            var account = await _accountRepository.GetByIdAsync(accountId);

            // Permission: only a teacher, and only for their own assigned course.
            // CourseId from the form is never trusted: it must match the teacher's course.
            if (account == null || account.Role != TeacherRole || account.CourseId == null
                || account.CourseId.Value != dto.CourseId)
            {
                throw new Exception(WrongCourseMessage);
            }

            var course = await _courseRepository.GetByIdAsync(dto.CourseId);

            if (course == null)
            {
                throw new Exception(WrongCourseMessage);
            }

            if (dto.File == null || dto.File.Length == 0)
            {
                throw new Exception("Please choose a file to upload.");
            }

            var extension = Path.GetExtension(dto.File.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                throw new Exception("Only PDF, DOCX, PPTX, and TXT files are allowed.");
            }

            // Compute the content hash before touching disk so a duplicate upload never
            // creates a stray physical file. Duplicate detection is by content, not name.
            var fileHashSha256 = await ComputeFileHashAsync(dto.File);

            var existingDuplicate =
                await _documentRepository.GetActiveByCourseAndFileHashAsync(course.CourseId, fileHashSha256);

            if (existingDuplicate != null)
            {
                throw new Exception(DuplicateFileMessage);
            }

            var folder = _configuration["FileStorage:DocumentFolder"];

            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new Exception("Document storage folder is not configured.");
            }

            Directory.CreateDirectory(folder);

            var storedFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(folder, storedFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await dto.File.CopyToAsync(stream);
            }

            var document = new Document
            {
                Title = dto.Title,
                Description = dto.Description,
                CourseId = course.CourseId,
                CourseCode = course.Code, // derived from the validated course, not the form
                Chapter = dto.Chapter,
                OriginalFileName = dto.File.FileName,
                StoredFileName = storedFileName,
                FilePath = fullPath,
                FileType = extension,
                FileHashSha256 = fileHashSha256,
                ContentType = dto.File.ContentType,
                FileSize = dto.File.Length,
                UploadStatus = "Approved",
                IndexStatus = "Processing",
                SubmittedByAccountId = accountId,
                SubmittedByEmail = email,
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.AddAsync(document);

            try
            {
                await _documentRepository.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition: another concurrent upload inserted the same content
                // first and the unique filtered index rejected this row. Roll back the
                // physical file and surface the same friendly duplicate message.
                TryDeleteFile(fullPath);
                throw new Exception(DuplicateFileMessage);
            }

            await AddLogAsync(document.DocumentId, "Upload", "Success", accountId, email, null, null);

            // MVC-side chunk preview from the saved file (no embeddings / vector data).
            var chunkOptions = await GetChunkPreviewOptionsAsync();
            await GeneratePreviewChunksAsync(document, fullPath, extension, accountId, email, chunkOptions);

            // Existing RAG indexing call (unchanged contract).
            try
            {
                var ragResponse = await _ragClient.IndexDocumentAsync(
                    CreateRagIndexRequest(document, chunkOptions));

                document.IndexStatus = "Indexed";
                document.TotalChunks = ragResponse.TotalChunks;
                document.IndexedAt = DateTime.UtcNow;
                document.IndexError = null;

                _documentRepository.Update(document);
                await _documentRepository.SaveChangesAsync();

                await AddLogAsync(document.DocumentId, "Index", "Success", accountId, email,
                    ragResponse.TotalChunks, null);
            }
            catch (Exception ex)
            {
                // Preview chunks (if any) are intentionally kept.
                document.IndexStatus = "Failed";
                document.IndexError = ex.Message;

                _documentRepository.Update(document);
                await _documentRepository.SaveChangesAsync();

                await AddLogAsync(document.DocumentId, "Index", "Failed", accountId, email, null, ex.Message);
            }

            return document.DocumentId;
        }

        public async Task<DocumentDetailsDto?> GetDetailsAsync(int documentId, int? accountId, string roleName)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);

            if (document == null)
            {
                return null;
            }

            var isAdmin = roleName == "Admin";
            var teacherCanAccess = !isAdmin && await TeacherCanAccessAsync(document, accountId, roleName);
            var studentCanAccess = roleName == "Student" && accountId != null
                && document.IndexStatus == "Indexed"
                && await StudentCanAccessAsync(document, accountId.Value);

            if (!isAdmin && !teacherCanAccess && !studentCanAccess)
            {
                return null;
            }

            await TryEnsurePreviewChunksAsync(document, accountId);

            var chunks = await _chunkRepository.GetByDocumentAsync(documentId);
            var logs = await _indexLogRepository.GetByDocumentAsync(documentId);

            string? previewMessage = null;
            if (chunks.Count == 0)
            {
                var failedPreview = logs
                    .FirstOrDefault(l => l.Action == "Preview" && l.Status == "Failed");
                previewMessage = failedPreview?.ErrorMessage;
            }

            return new DocumentDetailsDto
            {
                DocumentId = document.DocumentId,
                Title = document.Title,
                Description = document.Description,
                CourseCode = document.CourseCode,
                CourseName = document.Course?.Name,
                Chapter = document.Chapter,
                OriginalFileName = document.OriginalFileName,
                FileType = document.FileType,
                FileSize = document.FileSize,
                UploadStatus = document.UploadStatus,
                IndexStatus = document.IndexStatus,
                TotalChunks = document.TotalChunks,
                IndexError = document.IndexError,
                SubmittedByAccountId = document.SubmittedByAccountId,
                SubmittedByFullName = document.SubmittedByAccount?.FullName,
                SubmittedByEmail = document.SubmittedByEmail,
                CreatedAt = document.CreatedAt,
                IndexedAt = document.IndexedAt,
                PreviewMessage = previewMessage,
                CanReIndex = isAdmin || teacherCanAccess,
                Chunks = chunks.Select(c => new DocumentChunkDto
                {
                    ChunkIndex = c.ChunkIndex,
                    PageNumber = c.PageNumber,
                    ChunkText = c.ChunkText,
                    CharCount = c.CharCount,
                    TokenEstimate = c.TokenEstimate
                }).ToList(),
                IndexLogs = logs.Select(l => new DocumentIndexLogDto
                {
                    DocumentIndexLogId = l.DocumentIndexLogId,
                    Action = l.Action,
                    Status = l.Status,
                    PerformedByAccountId = l.PerformedByAccountId,
                    PerformedByEmail = l.PerformedByEmail,
                    PerformedAt = l.PerformedAt,
                    TotalChunks = l.TotalChunks,
                    ErrorMessage = l.ErrorMessage
                }).ToList()
            };
        }

        public async Task ReIndexAsync(int documentId, int? accountId, string email, string roleName)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);

            if (document == null)
            {
                throw new Exception("Document not found.");
            }

            var isAdmin = roleName == "Admin";

            if (!isAdmin && !await TeacherCanAccessAsync(document, accountId, roleName))
            {
                throw new Exception(WrongCourseMessage);
            }

            document.IndexStatus = "Processing";
            _documentRepository.Update(document);
            await _documentRepository.SaveChangesAsync();

            // Remove old preview chunks first so re-index never duplicates SQL chunks.
            await _chunkRepository.DeleteByDocumentAsync(documentId);
            await _chunkRepository.SaveChangesAsync();

            var chunkOptions = await GetChunkPreviewOptionsAsync();
            await GeneratePreviewChunksAsync(document, document.FilePath, document.FileType, accountId, email, chunkOptions);

            try
            {
                var ragResponse = await _ragClient.IndexDocumentAsync(
                    CreateRagIndexRequest(document, chunkOptions));

                document.IndexStatus = "Indexed";
                document.TotalChunks = ragResponse.TotalChunks;
                document.IndexedAt = DateTime.UtcNow;
                document.IndexError = null;

                _documentRepository.Update(document);
                await _documentRepository.SaveChangesAsync();

                await AddLogAsync(documentId, "ReIndex", "Success", accountId, email,
                    ragResponse.TotalChunks, null);
            }
            catch (Exception ex)
            {
                document.IndexStatus = "Failed";
                document.IndexError = ex.Message;

                _documentRepository.Update(document);
                await _documentRepository.SaveChangesAsync();

                await AddLogAsync(documentId, "ReIndex", "Failed", accountId, email, null, ex.Message);
            }
        }

        // ----------------------------------------------------------------- //
        // Helpers
        // ----------------------------------------------------------------- //
        private static async Task<string> ComputeFileHashAsync(Microsoft.AspNetCore.Http.IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var sha = SHA256.Create();
            var hashBytes = await sha.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort cleanup; never mask the original error.
            }
        }

        private async Task GeneratePreviewChunksAsync(
            Document document,
            string filePath,
            string fileType,
            int? accountId,
            string email,
            ChunkPreviewOptions? options = null)
        {
            try
            {
                var effectiveOptions = options ?? await GetChunkPreviewOptionsAsync();
                var preview = _chunkPreviewGenerator.Generate(filePath, fileType, effectiveOptions);

                if (!preview.Success || preview.Items.Count == 0)
                {
                    await AddLogAsync(document.DocumentId, "Preview", "Failed", accountId, email,
                        0, preview.ErrorMessage ?? "No text could be extracted for chunk preview.");
                    return;
                }

                var now = DateTime.UtcNow;
                var chunks = preview.Items.Select(i => new DocumentChunk
                {
                    DocumentId = document.DocumentId,
                    ChunkIndex = i.ChunkIndex,
                    PageNumber = i.PageNumber,
                    ChunkText = i.ChunkText,
                    CharCount = i.CharCount,
                    TokenEstimate = i.TokenEstimate,
                    CreatedAt = now
                });

                await _chunkRepository.AddRangeAsync(chunks);
                await _chunkRepository.SaveChangesAsync();

                await AddLogAsync(document.DocumentId, "Preview", "Success", accountId, email,
                    preview.Items.Count, null);
            }
            catch (Exception ex)
            {
                // Preview must never break the upload/indexing flow.
                await AddLogAsync(document.DocumentId, "Preview", "Failed", accountId, email,
                    0, $"Chunk preview generation failed: {ex.Message}");
            }
        }

        private async Task<ChunkPreviewOptions> GetChunkPreviewOptionsAsync()
        {
            var config = await _chunkConfigRepository.GetActiveAsync();

            if (config == null)
            {
                return ChunkPreviewOptions.Default;
            }

            return new ChunkPreviewOptions
            {
                ChunkMode = config.ChunkMode,
                ChunkSize = config.ChunkSize,
                ChunkOverlap = config.ChunkOverlap,
                MinChunkLength = config.MinChunkLength,
                MaxPreviewChunks = config.MaxPreviewChunks
            };
        }

        private static RagIndexRequest CreateRagIndexRequest(
            Document document,
            ChunkPreviewOptions chunkOptions)
        {
            return new RagIndexRequest
            {
                DocumentId = document.DocumentId.ToString(),
                CourseCode = document.CourseCode,
                Chapter = document.Chapter,
                FilePath = document.FilePath,
                FileName = document.OriginalFileName,
                ChunkMode = chunkOptions.ChunkMode,
                ChunkSize = chunkOptions.ChunkSize,
                ChunkOverlap = chunkOptions.ChunkOverlap,
                MinChunkLength = chunkOptions.MinChunkLength,
                // MaxPreviewChunks limits how much SQL preview/admin UI stores.
                // RAG indexing must cover the whole PDF so later pages remain
                // searchable and citations can still point to the right page.
                MaxPreviewChunks = RagIndexMaxChunks
            };
        }

        /// <summary>
        /// Backfill SQL preview chunks when RAG indexing succeeded but preview rows
        /// were never stored (legacy uploads or failed preview at upload time).
        /// </summary>
        private async Task TryEnsurePreviewChunksAsync(Document document, int? accountId)
        {
            if (await _chunkRepository.CountByDocumentAsync(document.DocumentId) > 0)
            {
                return;
            }

            if (document.TotalChunks <= 0 && !string.Equals(document.IndexStatus, "Indexed", StringComparison.Ordinal))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(document.FilePath) || !File.Exists(document.FilePath))
            {
                return;
            }

            await GeneratePreviewChunksAsync(
                document,
                document.FilePath,
                document.FileType,
                accountId,
                document.SubmittedByEmail ?? string.Empty);
        }

        private async Task<bool> TeacherCanAccessAsync(Document document, int? accountId, string roleName)
        {
            if (roleName != "Teacher" || accountId == null)
            {
                return false;
            }

            if (document.SubmittedByAccountId == accountId)
            {
                return true;
            }

            var account = await _accountRepository.GetByIdAsync(accountId.Value);
            return account?.CourseId != null && account.CourseId.Value == document.CourseId;
        }

        private async Task<bool> StudentCanAccessAsync(Document document, int accountId)
        {
            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account == null)
            {
                return false;
            }

            if (account.CourseId.HasValue)
            {
                return account.CourseId.Value == document.CourseId;
            }

            // No course on account: same scope as Student Library when session has no course filter.
            return document.IndexStatus == "Indexed";
        }

        private async Task AddLogAsync(
            int documentId, string action, string status, int? accountId, string email,
            int? totalChunks, string? errorMessage)
        {
            await _indexLogRepository.AddAsync(new DocumentIndexLog
            {
                DocumentId = documentId,
                Action = action,
                Status = status,
                PerformedByAccountId = accountId,
                PerformedByEmail = email ?? string.Empty,
                PerformedAt = DateTime.UtcNow,
                TotalChunks = totalChunks,
                ErrorMessage = errorMessage
            });

            await _indexLogRepository.SaveChangesAsync();
        }
    }
}
