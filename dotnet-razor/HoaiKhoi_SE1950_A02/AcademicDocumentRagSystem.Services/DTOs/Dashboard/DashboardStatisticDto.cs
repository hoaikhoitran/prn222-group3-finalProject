using System.Collections.Generic;

namespace AcademicDocumentRagSystem.Services.DTOs.Dashboard;

public class DashboardStatisticDto
{
    // Master data — always all-time (labeled as such on the UI).
    public int TotalAccounts { get; set; }
    public int TotalCourses { get; set; }

    // Activity metrics — respect the selected month/year filter.
    public int TotalDocuments { get; set; }
    public int TotalChatSessions { get; set; }
    public int TotalChatMessages { get; set; }
    public int TotalVectorChunks { get; set; }

    // Trạng thái (of documents created in the selected period)
    public int DocsPending { get; set; }
    public int DocsProcessing { get; set; }
    public int DocsIndexed { get; set; }
    public int DocsFailed { get; set; }

    // Real LLM token usage, summed from provider-reported usage on
    // ChatMessages in the selected period. Never estimated.
    public long TotalPromptTokens { get; set; }
    public long TotalCompletionTokens { get; set; }
    public long TotalLlmTokens { get; set; }

    /// <summary>Messages in the period that predate token tracking or had no provider usage.</summary>
    public int MessagesWithoutUsage { get; set; }

    /// <summary>
    /// Sum of DocumentChunk.TokenEstimate for chunks created in the period.
    /// This is a CONTENT-SIZE ESTIMATE, not real provider usage — it is shown
    /// separately and never folded into <see cref="TotalLlmTokens"/>.
    /// </summary>
    public long ChunkTokenEstimate { get; set; }

    /// <summary>
    /// System-wide token total: real Gemini tokens plus the chunk content
    /// estimate. Computed, never persisted; TotalLlmTokens stays Gemini-only.
    /// </summary>
    public long TotalSystemTokens =>
        TotalLlmTokens + ChunkTokenEstimate;

    /// <summary>
    /// System-token series over time for the selected filter (daily buckets
    /// for a month, monthly buckets otherwise). Purely additive — computed
    /// from the same period filter and never alters the totals above.
    /// </summary>
    public List<TokenTimelinePointDto> TokenTimeline { get; set; } = new();

    public List<CourseActivityReportDto> CourseReports { get; set; } = new();

    /// <summary>Real token usage per account, sorted by TotalTokens descending.</summary>
    public List<UserTokenUsageDto> UserTokenReports { get; set; } = new();

    /// <summary>Years that actually contain data (for the filter dropdown).</summary>
    public List<int> AvailableYears { get; set; } = new();
}

public class CourseActivityReportDto
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public int ChunkCount { get; set; }
    public int ChatSessionCount { get; set; }
    public int ChatMessageCount { get; set; }

    /// <summary>Real LLM tokens used by chats in this course during the period.</summary>
    public long TotalTokens { get; set; }
}
