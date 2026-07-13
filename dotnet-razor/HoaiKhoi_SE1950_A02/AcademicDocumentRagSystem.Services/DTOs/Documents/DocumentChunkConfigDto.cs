using System;

namespace AcademicDocumentRagSystem.Services.DTOs.Documents
{
    public class DocumentChunkConfigDto
    {
        public int DocumentChunkConfigId { get; set; }

        public string ChunkMode { get; set; } = "Characters";

        public int ChunkSize { get; set; } = 1500;

        public int ChunkOverlap { get; set; } = 250;

        public int MinChunkLength { get; set; } = 80;

        public int MaxPreviewChunks { get; set; } = 200;

        public bool IsActive { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public int? UpdatedByAccountId { get; set; }

        public string? UpdatedByFullName { get; set; }

        public string DisplaySummary =>
            ChunkMode == "Paragraph"
                ? $"{ChunkSize} paragraphs/chunk, overlap {ChunkOverlap}"
                : $"{ChunkSize} {ChunkMode.ToLowerInvariant()}/chunk, overlap {ChunkOverlap}";
    }
}
