using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.Services.Interfaces;
using AcademicDocumentRagSystem.Services.DTOs.Dashboard;

namespace AcademicDocumentRagSystem.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly AcademicRagDbContext _context;

    public DashboardService(AcademicRagDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatisticDto> GetAdminDashboardStatsAsync()
    {
        var stats = new DashboardStatisticDto();

        stats.TotalAccounts = await _context.Accounts.CountAsync();
        stats.TotalCourses = await _context.Courses.CountAsync();
        stats.TotalDocuments = await _context.Documents.Where(d => d.UploadStatus != "Deleted").CountAsync();
        stats.TotalChatSessions = await _context.ChatSessions.CountAsync();
        stats.TotalChatMessages = await _context.ChatMessages.CountAsync();
        stats.TotalVectorChunks = await _context.DocumentChunks.CountAsync();
        stats.TotalEmbeddingTokens = await _context.DocumentChunks.SumAsync(c => c.TokenEstimate ?? 0);
        stats.TotalChatTokens = await _context.ChatMessages.SumAsync(m => ((m.Question.Length + m.Answer.Length) / 4));

        var indexStatusGroups = await _context.Documents
            .Where(d => d.UploadStatus != "Deleted")
            .GroupBy(d => d.IndexStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var group in indexStatusGroups)
        {
            var status = group.Status ?? "Pending";
            if (status == "Pending") stats.DocsPending = group.Count;
            else if (status == "Processing") stats.DocsProcessing = group.Count;
            else if (status == "Indexed") stats.DocsIndexed = group.Count;
            else if (status == "Failed") stats.DocsFailed = group.Count;
        }

        stats.CourseReports = await _context.Courses
            .Select(c => new CourseActivityReportDto
            {
                CourseCode = c.Code ?? string.Empty,
                CourseName = c.Name ?? string.Empty,
                DocumentCount = c.Documents.Count(d => d.UploadStatus != "Deleted"),
                ChunkCount = c.Documents
                    .Where(d => d.UploadStatus != "Deleted")
                    .SelectMany(d => d.DocumentChunks)
                    .Count(),

                ChatSessionCount = c.ChatSessions.Count
            })
            .OrderByDescending(r => r.ChatSessionCount)
            .Take(10)
            .ToListAsync();

        return stats;
    }
}