using AcademicDocumentRagSystem.Services.DTOs.Documents;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.Services.Interfaces
{
    public interface IDocumentChunkConfigService
    {
        Task<DocumentChunkConfigDto> GetActiveAsync();

        Task<List<DocumentChunkConfigDto>> GetHistoryAsync();

        Task<DocumentChunkConfigDto> SaveAsync(UpdateDocumentChunkConfigDto dto, int? updatedByAccountId);
    }
}
