using AcademicDocumentRagSystem.DataAccess.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces
{
    public interface IDocumentChunkConfigRepository
    {
        Task<DocumentChunkConfig?> GetActiveAsync();

        Task<DocumentChunkConfig?> GetByIdAsync(int id);

        Task<List<DocumentChunkConfig>> GetHistoryAsync(int take = 20);

        Task AddAsync(DocumentChunkConfig config);

        void Update(DocumentChunkConfig config);

        Task DeactivateAllAsync();

        Task SaveChangesAsync();
    }
}
