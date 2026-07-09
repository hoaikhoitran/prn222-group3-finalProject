using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace AcademicDocumentRagSystem.Services.DTOs.Documents
{
    public class UpdateDocumentChunkConfigDto : IValidatableObject
    {
        public static readonly string[] SupportedModes = { "Characters", "Words", "Paragraph" };

        [Required]
        public string ChunkMode { get; set; } = "Characters";

        [Range(1, 10000)]
        public int ChunkSize { get; set; } = 1500;

        [Range(0, 5000)]
        public int ChunkOverlap { get; set; } = 250;

        [Range(0, 2000)]
        public int MinChunkLength { get; set; } = 80;

        [Range(1, 1000)]
        public int MaxPreviewChunks { get; set; } = 200;

        [StringLength(1000)]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!SupportedModes.Contains(ChunkMode))
            {
                yield return new ValidationResult(
                    "Chunk mode must be Characters, Words, or Paragraph.",
                    new[] { nameof(ChunkMode) });
            }

            if (ChunkMode == "Characters" && (ChunkSize < 300 || ChunkSize > 6000))
            {
                yield return new ValidationResult(
                    "Character chunk size should be between 300 and 6000.",
                    new[] { nameof(ChunkSize) });
            }

            if (ChunkMode == "Words" && (ChunkSize < 80 || ChunkSize > 1500))
            {
                yield return new ValidationResult(
                    "Word chunk size should be between 80 and 1500.",
                    new[] { nameof(ChunkSize) });
            }

            if (ChunkMode == "Paragraph" && (ChunkSize < 1 || ChunkSize > 20))
            {
                yield return new ValidationResult(
                    "Paragraph chunk size should be between 1 and 20.",
                    new[] { nameof(ChunkSize) });
            }

            if (ChunkOverlap >= ChunkSize)
            {
                yield return new ValidationResult(
                    "Overlap must be smaller than chunk size.",
                    new[] { nameof(ChunkOverlap) });
            }
        }
    }
}
