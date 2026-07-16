namespace AcademicDocumentRagSystem.Services.DTOs.Dashboard;

/// <summary>
/// Per-account token report combining two independent sources:
/// real Gemini usage for the questions the account ASKED
/// (ChatMessage.AccountId) and the local content-size estimate of the
/// chunks belonging to documents the account UPLOADED
/// (Document.SubmittedByAccountId). The two are queried separately and
/// merged by AccountId — never joined — so neither side can double count.
/// </summary>
public sealed class UserTokenUsageDto
{
    public int AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Number of questions the account asked in the selected period.</summary>
    public int MessageCount { get; set; }

    /// <summary>Messages in the period that have no provider usage data (old history, mock answers).</summary>
    public int MissingUsageCount { get; set; }

    /// <summary>Gemini input tokens (provider-reported) for the account's questions.</summary>
    public long PromptTokens { get; set; }

    /// <summary>Gemini answer tokens (provider-reported) for the account's questions.</summary>
    public long CompletionTokens { get; set; }

    /// <summary>Real Gemini token total (provider-reported). Already includes prompt + completion.</summary>
    public long TotalTokens { get; set; }

    /// <summary>Chunks created (in the period) from documents this account uploaded.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Locally estimated content tokens of those chunks. An estimate, not provider usage.</summary>
    public long ChunkTokenEstimate { get; set; }

    /// <summary>
    /// System-wide token total for the account: real Gemini total plus the
    /// chunk content estimate. Never add PromptTokens/CompletionTokens on
    /// top — TotalTokens already covers them.
    /// </summary>
    public long CombinedTotalTokens =>
        TotalTokens + ChunkTokenEstimate;
}
