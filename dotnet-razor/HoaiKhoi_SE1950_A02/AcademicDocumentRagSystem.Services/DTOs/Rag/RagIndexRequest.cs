using System.Text.Json.Serialization;

namespace AcademicDocumentRagSystem.Services.DTOs.Rag;

public class RagIndexRequest
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("courseCode")]
    public string CourseCode { get; set; } = string.Empty;

    [JsonPropertyName("chapter")]
    public string? Chapter { get; set; }

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("chunkMode")]
    public string ChunkMode { get; set; } = "Characters";

    [JsonPropertyName("chunkSize")]
    public int ChunkSize { get; set; } = 1500;

    [JsonPropertyName("chunkOverlap")]
    public int ChunkOverlap { get; set; } = 250;

    [JsonPropertyName("minChunkLength")]
    public int MinChunkLength { get; set; } = 80;

    [JsonPropertyName("maxPreviewChunks")]
    public int MaxPreviewChunks { get; set; } = 200;
}
