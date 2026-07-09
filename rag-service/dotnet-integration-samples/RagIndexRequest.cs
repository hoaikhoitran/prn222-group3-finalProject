// =======================================================================
// RagIndexRequest.cs
// -----------------------------------------------------------------------
// Request DTO for POST /rag/index-document on the Python RAG service.
// Copy this file into your PRN222 .NET project (e.g. PRN222.Application/
// RagService/Dto/).
// Property names match the JSON expected by the Python service.
// =======================================================================
using System.Text.Json.Serialization;

namespace PRN222.Integration.Rag;

public class RagIndexRequest
{
    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("courseCode")]
    public string CourseCode { get; set; } = string.Empty;

    [JsonPropertyName("chapter")]
    public string Chapter { get; set; } = string.Empty;

    /// <summary>
    /// Local path to the document as seen by the Python service.
    /// In a docker-compose setup, mount the same folder on both sides
    /// so this path works in the Python container.
    /// </summary>
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
