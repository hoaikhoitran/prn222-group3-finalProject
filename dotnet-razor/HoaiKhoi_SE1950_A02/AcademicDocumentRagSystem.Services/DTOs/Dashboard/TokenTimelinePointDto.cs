using System;

namespace AcademicDocumentRagSystem.Services.DTOs.Dashboard;

/// <summary>
/// One bucket of the system-token timeline. Buckets are contiguous — days of
/// the selected month, months of the selected year, or every month from the
/// first data point to the current UTC month — and buckets without data are
/// emitted with zeros so the chart line never skips a period.
/// </summary>
public sealed class TokenTimelinePointDto
{
    /// <summary>Axis label: "dd/MM" for daily buckets, "MM/yyyy" for monthly ones.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Inclusive UTC start of the bucket (start &lt;= CreatedAt &lt; next bucket).</summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>Real Gemini tokens (ChatMessage.TotalTokens) created in the bucket.</summary>
    public long LlmTokens { get; set; }

    /// <summary>Locally estimated chunk content tokens (DocumentChunk.TokenEstimate) created in the bucket.</summary>
    public long ChunkTokenEstimate { get; set; }

    /// <summary>LlmTokens + ChunkTokenEstimate — same formula as DashboardStatisticDto.TotalSystemTokens.</summary>
    public long TotalSystemTokens { get; set; }
}
