using System;
using System.Collections.Generic;
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

    public async Task<DashboardStatisticDto> GetAdminDashboardStatsAsync(DashboardFilterDto? filter = null)
    {
        filter ??= new DashboardFilterDto();
        var start = filter.UtcStart;
        var end = filter.UtcEnd;

        var stats = new DashboardStatisticDto();

        // Master data: always all-time (the UI labels these as such).
        stats.TotalAccounts = await _context.Accounts.CountAsync();
        stats.TotalCourses = await _context.Courses.CountAsync();

        // Activity metrics: inclusive start, exclusive end, all in UTC —
        // the same convention the database uses (sysutcdatetime).
        var documents = ApplyPeriod(_context.Documents.Where(d => d.UploadStatus != "Deleted"), d => d.CreatedAt, start, end);
        var sessions = ApplyPeriod(_context.ChatSessions.AsQueryable(), s => s.CreatedAt, start, end);
        var messages = ApplyPeriod(_context.ChatMessages.AsQueryable(), m => m.CreatedAt, start, end);
        var chunks = ApplyPeriod(_context.DocumentChunks.AsQueryable(), c => c.CreatedAt, start, end);

        stats.TotalDocuments = await documents.CountAsync();
        stats.TotalChatSessions = await sessions.CountAsync();
        stats.TotalChatMessages = await messages.CountAsync();
        stats.TotalVectorChunks = await chunks.CountAsync();

        // Chunk token estimate: content-size estimate only, reported apart
        // from the real LLM usage and never added to it.
        stats.ChunkTokenEstimate = await chunks.SumAsync(c => (long)(c.TokenEstimate ?? 0));

        // Real LLM token usage. Messages without provider usage are counted,
        // not silently treated as zero-token calls.
        stats.TotalPromptTokens = await messages.SumAsync(m => (long)(m.PromptTokens ?? 0));
        stats.TotalCompletionTokens = await messages.SumAsync(m => (long)(m.CompletionTokens ?? 0));
        stats.TotalLlmTokens = await messages.SumAsync(m => (long)(m.TotalTokens ?? 0));
        stats.MessagesWithoutUsage = await messages.CountAsync(m => m.TotalTokens == null);

        var indexStatusGroups = await documents
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

        stats.CourseReports = await BuildCourseReportsAsync(start, end);
        stats.UserTokenReports = await BuildUserTokenReportsAsync(start, end);
        stats.AvailableYears = await BuildAvailableYearsAsync();

        return stats;
    }

    private static IQueryable<T> ApplyPeriod<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, DateTime>> createdAt,
        DateTime? start,
        DateTime? end)
    {
        if (!start.HasValue || !end.HasValue)
        {
            return query;
        }

        // start <= CreatedAt && CreatedAt < end, built as an expression so
        // EF Core still translates it to SQL.
        var parameter = createdAt.Parameters[0];
        var body = System.Linq.Expressions.Expression.AndAlso(
            System.Linq.Expressions.Expression.GreaterThanOrEqual(
                createdAt.Body, System.Linq.Expressions.Expression.Constant(start.Value)),
            System.Linq.Expressions.Expression.LessThan(
                createdAt.Body, System.Linq.Expressions.Expression.Constant(end.Value)));
        var predicate = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);

        return query.Where(predicate);
    }

    private async Task<List<CourseActivityReportDto>> BuildCourseReportsAsync(DateTime? start, DateTime? end)
    {
        var hasPeriod = start.HasValue && end.HasValue;
        var s = start ?? DateTime.MinValue;
        var e = end ?? DateTime.MaxValue;

        return await _context.Courses
            .Select(c => new CourseActivityReportDto
            {
                CourseCode = c.Code ?? string.Empty,
                CourseName = c.Name ?? string.Empty,
                DocumentCount = c.Documents.Count(d =>
                    d.UploadStatus != "Deleted" &&
                    (!hasPeriod || (d.CreatedAt >= s && d.CreatedAt < e))),
                ChunkCount = c.Documents
                    .Where(d => d.UploadStatus != "Deleted")
                    .SelectMany(d => d.DocumentChunks)
                    .Count(ch => !hasPeriod || (ch.CreatedAt >= s && ch.CreatedAt < e)),
                ChatSessionCount = c.ChatSessions.Count(cs =>
                    !hasPeriod || (cs.CreatedAt >= s && cs.CreatedAt < e)),
                ChatMessageCount = c.ChatSessions
                    .SelectMany(cs => cs.ChatMessages)
                    .Count(m => !hasPeriod || (m.CreatedAt >= s && m.CreatedAt < e)),
                TotalTokens = c.ChatSessions
                    .SelectMany(cs => cs.ChatMessages)
                    .Where(m => !hasPeriod || (m.CreatedAt >= s && m.CreatedAt < e))
                    .Sum(m => (long)(m.TotalTokens ?? 0))
            })
            .OrderByDescending(r => r.ChatSessionCount)
            .Take(10)
            .ToListAsync();
    }

    private async Task<List<UserTokenUsageDto>> BuildUserTokenReportsAsync(DateTime? start, DateTime? end)
    {
        var messages = ApplyPeriod(_context.ChatMessages.AsQueryable(), m => m.CreatedAt, start, end);

        // Tokens are attributed to the account that asked (ChatMessage.AccountId).
        var usageRows = await messages
            .GroupBy(m => m.AccountId)
            .Select(g => new UserTokenUsageDto
            {
                AccountId = g.Key,
                MessageCount = g.Count(),
                MissingUsageCount = g.Count(m => m.TotalTokens == null),
                PromptTokens = g.Sum(m => (long)(m.PromptTokens ?? 0)),
                CompletionTokens = g.Sum(m => (long)(m.CompletionTokens ?? 0)),
                TotalTokens = g.Sum(m => (long)(m.TotalTokens ?? 0))
            })
            .ToListAsync();

        if (usageRows.Count == 0)
        {
            return usageRows;
        }

        // Left-join account info separately so a physically deleted account
        // (legacy data) can never break the report.
        var accountIds = usageRows.Select(r => r.AccountId).ToList();
        var accounts = await _context.Accounts
            .Where(a => accountIds.Contains(a.AccountId))
            .Select(a => new { a.AccountId, a.FullName, a.Email, a.Role })
            .ToDictionaryAsync(a => a.AccountId);

        foreach (var row in usageRows)
        {
            if (accounts.TryGetValue(row.AccountId, out var account))
            {
                row.FullName = account.FullName;
                row.Email = account.Email;
                row.RoleName = account.Role == 1 ? "Student"
                    : account.Role == 2 ? "Teacher"
                    : $"Role {account.Role}";
            }
            else
            {
                row.FullName = $"Tài khoản #{row.AccountId} (đã xóa)";
                row.Email = string.Empty;
                row.RoleName = "—";
            }
        }

        return usageRows
            .OrderByDescending(r => r.TotalTokens)
            .ThenByDescending(r => r.MessageCount)
            .ToList();
    }

    private async Task<List<int>> BuildAvailableYearsAsync()
    {
        var currentYear = DateTime.UtcNow.Year;
        var minYear = currentYear;

        // The earliest activity in the system defines the first selectable year.
        if (await _context.Documents.AnyAsync())
        {
            minYear = Math.Min(minYear, (await _context.Documents.MinAsync(d => d.CreatedAt)).Year);
        }

        if (await _context.ChatMessages.AnyAsync())
        {
            minYear = Math.Min(minYear, (await _context.ChatMessages.MinAsync(m => m.CreatedAt)).Year);
        }

        if (await _context.Accounts.AnyAsync())
        {
            minYear = Math.Min(minYear, (await _context.Accounts.MinAsync(a => a.CreatedAt)).Year);
        }

        minYear = Math.Max(minYear, DashboardFilterDto.MinYear);

        return Enumerable.Range(minYear, currentYear - minYear + 1)
            .Reverse()
            .ToList();
    }
}
