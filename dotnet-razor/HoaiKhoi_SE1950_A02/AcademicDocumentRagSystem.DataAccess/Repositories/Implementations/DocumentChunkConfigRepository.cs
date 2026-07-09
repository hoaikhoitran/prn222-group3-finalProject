using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.DataAccess.Repositories.Implementations
{
    public class DocumentChunkConfigRepository : IDocumentChunkConfigRepository
    {
        private readonly AcademicRagDbContext _context;

        public DocumentChunkConfigRepository(AcademicRagDbContext context)
        {
            _context = context;
        }

        public async Task<DocumentChunkConfig?> GetActiveAsync()
        {
            return await _context.DocumentChunkConfigs
                .Include(c => c.UpdatedByAccount)
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<DocumentChunkConfig?> GetByIdAsync(int id)
        {
            return await _context.DocumentChunkConfigs
                .Include(c => c.UpdatedByAccount)
                .FirstOrDefaultAsync(c => c.DocumentChunkConfigId == id);
        }

        public async Task<List<DocumentChunkConfig>> GetHistoryAsync(int take = 20)
        {
            return await _context.DocumentChunkConfigs
                .Include(c => c.UpdatedByAccount)
                .OrderByDescending(c => c.UpdatedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task AddAsync(DocumentChunkConfig config)
        {
            await _context.DocumentChunkConfigs.AddAsync(config);
        }

        public void Update(DocumentChunkConfig config)
        {
            _context.DocumentChunkConfigs.Update(config);
        }

        public async Task DeactivateAllAsync()
        {
            var activeConfigs = await _context.DocumentChunkConfigs
                .Where(c => c.IsActive)
                .ToListAsync();

            foreach (var config in activeConfigs)
            {
                config.IsActive = false;
                _context.DocumentChunkConfigs.Update(config);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
