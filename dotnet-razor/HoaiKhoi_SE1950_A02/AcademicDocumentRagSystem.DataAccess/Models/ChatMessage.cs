using System;
using System.Collections.Generic;

namespace AcademicDocumentRagSystem.DataAccess.Models;

public partial class ChatMessage
{
    public int ChatMessageId { get; set; }

    public int ChatSessionId { get; set; }

    public int AccountId { get; set; }

    public int DocumentId { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public string SourcesJson { get; set; } = null!;

    /// <summary>
    /// Prompt tokens reported by the LLM provider for this exchange.
    /// Null when the provider was not called (mock mode, provenance answers)
    /// or did not report usage — never an estimate.
    /// </summary>
    public int? PromptTokens { get; set; }

    /// <summary>Completion (answer) tokens reported by the LLM provider. Null when unavailable.</summary>
    public int? CompletionTokens { get; set; }

    /// <summary>
    /// Provider's official total token count. May exceed prompt + completion
    /// when the model spends internal reasoning tokens. Null when unavailable.
    /// </summary>
    public int? TotalTokens { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual ChatSession ChatSession { get; set; } = null!;

    public virtual Document Document { get; set; } = null!;
}
