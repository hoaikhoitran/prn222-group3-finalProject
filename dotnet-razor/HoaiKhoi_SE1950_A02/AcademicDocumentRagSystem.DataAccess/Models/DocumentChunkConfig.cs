using System;

namespace AcademicDocumentRagSystem.DataAccess.Models;

public partial class DocumentChunkConfig
{
    public int DocumentChunkConfigId { get; set; }

    public string ChunkMode { get; set; } = null!;

    public int ChunkSize { get; set; }

    public int ChunkOverlap { get; set; }

    public int MinChunkLength { get; set; }

    public int MaxPreviewChunks { get; set; }

    public bool IsActive { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? UpdatedByAccountId { get; set; }

    public virtual Account? UpdatedByAccount { get; set; }
}
