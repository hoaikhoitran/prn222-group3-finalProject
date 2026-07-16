using System.Text.Json.Serialization;

namespace AcademicDocumentRagSystem.Services.DTOs.Rag;

/// <summary>
/// Real token usage reported by the LLM provider for one /rag/ask call.
/// Values come straight from the provider's usage metadata (for Gemini:
/// usageMetadata.promptTokenCount / candidatesTokenCount / totalTokenCount).
/// All fields are null when the provider was not called or did not report
/// usage — they are never estimated from character or word counts.
/// </summary>
public class RagUsageDto
{
    [JsonPropertyName("promptTokens")]
    public int? PromptTokens { get; set; }

    [JsonPropertyName("completionTokens")]
    public int? CompletionTokens { get; set; }

    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; set; }
}
