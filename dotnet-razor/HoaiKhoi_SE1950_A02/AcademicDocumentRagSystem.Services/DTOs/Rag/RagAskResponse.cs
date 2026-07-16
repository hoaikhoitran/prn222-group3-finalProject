using System.Text.Json.Serialization;

namespace AcademicDocumentRagSystem.Services.DTOs.Rag
{
    public class RagAskResponse
    {
        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// Only the chunks the model actually cited in the answer
        /// (validated by the RAG service against the retrieved context).
        /// </summary>
        [JsonPropertyName("sources")]
        public List<RagSourceDto> Sources { get; set; } = new();

        /// <summary>
        /// Every chunk retrieval returned (superset of <see cref="Sources"/>).
        /// Kept for debugging; not shown under answers.
        /// </summary>
        [JsonPropertyName("retrievedSources")]
        public List<RagSourceDto> RetrievedSources { get; set; } = new();

        [JsonPropertyName("usedCitationIds")]
        public List<string> UsedCitationIds { get; set; } = new();

        /// <summary>Real provider token usage; null when unavailable.</summary>
        [JsonPropertyName("usage")]
        public RagUsageDto? Usage { get; set; }
    }
}