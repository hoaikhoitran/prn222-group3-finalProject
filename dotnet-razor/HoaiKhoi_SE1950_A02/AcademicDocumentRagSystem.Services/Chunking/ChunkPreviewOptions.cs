namespace AcademicDocumentRagSystem.Services.Chunking
{
    public class ChunkPreviewOptions
    {
        public string ChunkMode { get; set; } = "Characters";

        public int ChunkSize { get; set; } = 800;

        public int ChunkOverlap { get; set; } = 100;

        public int MinChunkLength { get; set; } = 50;

        public int MaxPreviewChunks { get; set; } = 10000;

        public static ChunkPreviewOptions Default => new();
    }
}
