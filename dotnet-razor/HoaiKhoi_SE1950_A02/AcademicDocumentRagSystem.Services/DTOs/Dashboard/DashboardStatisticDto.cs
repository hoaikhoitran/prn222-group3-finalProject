using System.Collections.Generic;

namespace AcademicDocumentRagSystem.Services.DTOs.Dashboard;

public class DashboardStatisticDto
{
    public int TotalAccounts { get; set; }
    public int TotalCourses { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalChatSessions { get; set; }
    public int TotalChatMessages { get; set; }
    public int TotalVectorChunks { get; set; }

    // Trạng thái
    public int DocsPending { get; set; }
    public int DocsProcessing { get; set; }
    public int DocsIndexed { get; set; }
    public int DocsFailed { get; set; }

    // Token
    public long TotalEmbeddingTokens { get; set; } 
    public long TotalChatTokens { get; set; }    
    public long TotalTokensUsed => TotalEmbeddingTokens + TotalChatTokens;

    public List<CourseActivityReportDto> CourseReports { get; set; } = new();
}

public class CourseActivityReportDto
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public int ChunkCount { get; set; }
    public int ChatSessionCount { get; set; }
}