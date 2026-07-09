namespace AcademicDocumentRagSystem.Services.Chunking
{
    /// <summary>
    /// Generates a human-readable preview of how a saved document is split into
    /// chunks. This runs entirely inside the MVC solution and does not call the
    /// Python RAG service or expose embeddings / vector data.
    /// </summary>
    public interface IChunkPreviewGenerator
    {
        /// <param name="filePath">Absolute path of the saved file on disk.</param>
        /// <param name="fileType">File extension including the dot, e.g. ".pdf".</param>
        ChunkPreviewResult Generate(string filePath, string fileType);

        /// <param name="filePath">Absolute path of the saved file on disk.</param>
        /// <param name="fileType">File extension including the dot, e.g. ".pdf".</param>
        /// <param name="options">Admin-managed preview chunking options.</param>
        ChunkPreviewResult Generate(string filePath, string fileType, ChunkPreviewOptions options);

        /// <param name="text">Plain text entered by an admin to test the current settings.</param>
        /// <param name="options">Admin-managed preview chunking options.</param>
        ChunkPreviewResult GenerateFromText(string text, ChunkPreviewOptions options);
    }
}
