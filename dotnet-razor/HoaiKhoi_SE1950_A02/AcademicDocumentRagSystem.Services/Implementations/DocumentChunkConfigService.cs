using AcademicDocumentRagSystem.DataAccess.Models;
using AcademicDocumentRagSystem.DataAccess.Repositories.Interfaces;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AcademicDocumentRagSystem.Services.Implementations
{
    public class DocumentChunkConfigService : IDocumentChunkConfigService
    {
        private readonly IDocumentChunkConfigRepository _configRepository;

        public DocumentChunkConfigService(IDocumentChunkConfigRepository configRepository)
        {
            _configRepository = configRepository;
        }

        public async Task<DocumentChunkConfigDto> GetActiveAsync()
        {
            var active = await _configRepository.GetActiveAsync();
            if (active != null)
            {
                return MapToDto(active);
            }

            return new DocumentChunkConfigDto
            {
                ChunkMode = "Characters",
                ChunkSize = 1500,
                ChunkOverlap = 250,
                MinChunkLength = 80,
                MaxPreviewChunks = 200,
                IsActive = true,
                Notes = "Default preview chunking configuration."
            };
        }

        public async Task<List<DocumentChunkConfigDto>> GetHistoryAsync()
        {
            var history = await _configRepository.GetHistoryAsync();
            return history.Select(MapToDto).ToList();
        }

        public async Task<DocumentChunkConfigDto> SaveAsync(
            UpdateDocumentChunkConfigDto dto,
            int? updatedByAccountId)
        {
            var now = DateTime.UtcNow;
            var config = new DocumentChunkConfig
            {
                ChunkMode = dto.ChunkMode,
                ChunkSize = dto.ChunkSize,
                ChunkOverlap = dto.ChunkOverlap,
                MinChunkLength = dto.MinChunkLength,
                MaxPreviewChunks = dto.MaxPreviewChunks,
                Notes = dto.Notes,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedByAccountId = updatedByAccountId
            };

            await _configRepository.AddAsOnlyActiveAsync(config);

            return MapToDto(config);
        }

        private static DocumentChunkConfigDto MapToDto(DocumentChunkConfig config)
        {
            return new DocumentChunkConfigDto
            {
                DocumentChunkConfigId = config.DocumentChunkConfigId,
                ChunkMode = config.ChunkMode,
                ChunkSize = config.ChunkSize,
                ChunkOverlap = config.ChunkOverlap,
                MinChunkLength = config.MinChunkLength,
                MaxPreviewChunks = config.MaxPreviewChunks,
                IsActive = config.IsActive,
                Notes = config.Notes,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt,
                UpdatedByAccountId = config.UpdatedByAccountId,
                UpdatedByFullName = config.UpdatedByAccount?.FullName
            };
        }
    }
}
