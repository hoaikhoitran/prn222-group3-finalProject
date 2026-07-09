namespace AcademicDocumentRagSystem.Services.Chunking
{
    public class ChunkPreviewOptions
    {
        public string ChunkMode { get; set; } = "Characters";

        public int ChunkSize { get; set; } = 1500;

        public int ChunkOverlap { get; set; } = 250;

        public int MinChunkLength { get; set; } = 80;

        public int MaxPreviewChunks { get; set; } = 200;

        public static ChunkPreviewOptions Default => new();
    }
}
