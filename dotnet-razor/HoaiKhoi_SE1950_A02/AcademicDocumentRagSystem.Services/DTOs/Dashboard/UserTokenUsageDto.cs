namespace AcademicDocumentRagSystem.Services.DTOs.Dashboard;

/// <summary>
/// Real LLM token usage aggregated per account (the account that ASKED the
/// question — ChatMessage.AccountId). Token numbers only sum provider-reported
/// usage; messages saved without usage metadata are counted separately in
/// <see cref="MissingUsageCount"/> instead of being treated as 0-token calls.
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

    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens { get; set; }
}
