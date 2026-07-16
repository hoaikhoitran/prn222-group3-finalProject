using System.Text.Json.Serialization;

namespace AcademicDocumentRagSystem.Services.DTOs.Rag;

public class RagSourceDto
{
    /// <summary>
    /// Per-request citation ID (C1, C2, ...) assigned by the RAG service in
    /// retrieval order. The answer text references chunks by these IDs.
    /// Empty for chat history saved before citation tracking existed.
    /// </summary>
    [JsonPropertyName("citationId")]
    public string CitationId { get; set; } = string.Empty;

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("pageNumber")]
    public int? PageNumber { get; set; }

    [JsonPropertyName("chunkIndex")]
    public int ChunkIndex { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("distance")]
    public double? Distance { get; set; }

    /// <summary>
    /// Uploader full name, enriched on the .NET side from SQL document metadata
    /// after the RAG service responds. Not produced by the Python RAG service.
    /// </summary>
    [JsonPropertyName("uploadedByFullName")]
    public string? UploadedByFullName { get; set; }

    /// <summary>Uploader email, enriched on the .NET side from SQL document metadata.</summary>
    [JsonPropertyName("uploadedByEmail")]
    public string? UploadedByEmail { get; set; }

    /// <summary>Document chapter, enriched on the .NET side from SQL document metadata.</summary>
    [JsonPropertyName("chapter")]
    public string? Chapter { get; set; }
}