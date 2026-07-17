using AcademicDocumentRagSystem.RazorPages.Infrastructure;
using AcademicDocumentRagSystem.Services.Chunking;
using AcademicDocumentRagSystem.Services.DTOs.Documents;
using AcademicDocumentRagSystem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AcademicDocumentRagSystem.RazorPages.Pages.Documents
{
    [SessionAuthorize("Admin")]
    public class ChunkConfigModel : PageModel
    {
        private readonly IDocumentChunkConfigService _chunkConfigService;
        private readonly IChunkPreviewGenerator _chunkPreviewGenerator;

        public ChunkConfigModel(
            IDocumentChunkConfigService chunkConfigService,
            IChunkPreviewGenerator chunkPreviewGenerator)
        {
            _chunkConfigService = chunkConfigService;
            _chunkPreviewGenerator = chunkPreviewGenerator;
        }

        [BindProperty]
        public UpdateDocumentChunkConfigDto Input { get; set; } = new();

        [BindProperty]
        public string SampleText { get; set; } = DefaultSampleText;

        public DocumentChunkConfigDto ActiveConfig { get; private set; } = new();

        public List<DocumentChunkConfigDto> History { get; private set; } = new();

        public List<ChunkPreviewItem> SamplePreview { get; private set; } = new();

        public string? PreviewError { get; private set; }

        public string[] Modes { get; } = UpdateDocumentChunkConfigDto.SupportedModes;

        public List<ChunkPresetViewModel> Presets { get; } = new()
        {
            new("Fast PDF / Reference", "Characters", 800, 100, 50, 10000,
                "Fast page-aware recursive chunks for PDFs and source citation review."),
            new("Precise Q&A", "Words", 280, 40, 60, 10000,
                "Smaller semantic windows for focused retrieval and short answers."),
            new("Lecture Notes", "Paragraph", 2, 0, 40, 10000,
                "Keeps structured paragraphs together for notes and outlines."),
            new("Dense Material", "Characters", 2200, 300, 120, 10000,
                "Larger chunks for long technical material with more surrounding context.")
        };

        public Dictionary<string, string> ModeGuidance { get; } = new()
        {
            ["Characters"] = "Recommended range: 800-3000 chars. Best for mixed file types and stable chunk size.",
            ["Words"] = "Recommended range: 150-500 words. Best when preserving word boundaries matters.",
            ["Paragraph"] = "Recommended range: 1-5 paragraphs. Best for lecture notes, outlines, and structured documents."
        };

        public async Task OnGetAsync()
        {
            await LoadAsync();
            Input = new UpdateDocumentChunkConfigDto
            {
                ChunkMode = ActiveConfig.ChunkMode,
                ChunkSize = ActiveConfig.ChunkSize,
                ChunkOverlap = ActiveConfig.ChunkOverlap,
                MinChunkLength = ActiveConfig.MinChunkLength,
                MaxPreviewChunks = ActiveConfig.MaxPreviewChunks,
                Notes = ActiveConfig.Notes
            };
        }

        public async Task<IActionResult> OnPostPreviewAsync()
        {
            await LoadAsync();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var preview = _chunkPreviewGenerator.GenerateFromText(SampleText, ToOptions(Input));

            if (!preview.Success)
            {
                PreviewError = preview.ErrorMessage;
                return Page();
            }

            SamplePreview = preview.Items;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadAsync();
                return Page();
            }

            var accountId = HttpContext.Session.GetInt32(SessionKeys.AccountId);

            try
            {
                await _chunkConfigService.SaveAsync(Input, accountId);
                TempData["Success"] = "Chunk configuration was saved. New uploads and re-index actions will use it.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await LoadAsync();
                return Page();
            }
        }

        private async Task LoadAsync()
        {
            ActiveConfig = await _chunkConfigService.GetActiveAsync();
            History = await _chunkConfigService.GetHistoryAsync();
        }

        private static ChunkPreviewOptions ToOptions(UpdateDocumentChunkConfigDto dto)
        {
            return new ChunkPreviewOptions
            {
                ChunkMode = dto.ChunkMode,
                ChunkSize = dto.ChunkSize,
                ChunkOverlap = dto.ChunkOverlap,
                MinChunkLength = dto.MinChunkLength,
                MaxPreviewChunks = dto.MaxPreviewChunks
            };
        }

        private const string DefaultSampleText =
            "Retrieval augmented generation works best when source documents are split into useful chunks.\n\n" +
            "A chunk should be large enough to keep context, but small enough for search to find the exact passage.\n\n" +
            "Overlap helps preserve meaning when a sentence or idea falls near the boundary between two chunks.\n\n" +
            "Paragraph chunking keeps author structure intact, while word and character chunking keep chunk sizes predictable.";
    }

    public record ChunkPresetViewModel(
        string Name,
        string Mode,
        int Size,
        int Overlap,
        int MinLength,
        int MaxPreviewChunks,
        string Description);
}
