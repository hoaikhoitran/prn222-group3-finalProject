using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using UglyToad.PdfPig;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocumentRagSystem.Services.Chunking
{
    public class ChunkPreviewGenerator : IChunkPreviewGenerator
    {
        // Splitting configuration (characters).
        private const int ChunkSize = 1500;
        private const int Overlap = 250;

        private const string ScanOnlyMessage =
            "Không trích xuất được text từ tài liệu này. Tài liệu có thể là bản scan hoặc ảnh.";

        public ChunkPreviewResult Generate(string filePath, string fileType)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return ChunkPreviewResult.Fail("Saved file was not found for chunk preview generation.");
            }

            var extension = (fileType ?? Path.GetExtension(filePath)).Trim().ToLowerInvariant();

            try
            {
                // Each "section" carries an optional page/slide number and its text.
                List<(int? PageNumber, string Text)> sections = extension switch
                {
                    ".txt" => ExtractTxt(filePath),
                    ".pdf" => ExtractPdf(filePath),
                    ".docx" => ExtractDocx(filePath),
                    ".pptx" => ExtractPptx(filePath),
                    _ => new List<(int?, string)>()
                };

                var hasText = sections.Any(s => !string.IsNullOrWhiteSpace(s.Text));

                if (!hasText)
                {
                    // For PDF this almost always means a scanned / image-only file.
                    return ChunkPreviewResult.Fail(ScanOnlyMessage);
                }

                var items = BuildChunks(sections);

                if (items.Count == 0)
                {
                    return ChunkPreviewResult.Fail(ScanOnlyMessage);
                }

                return ChunkPreviewResult.Ok(items);
            }
            catch (Exception ex)
            {
                return ChunkPreviewResult.Fail($"Chunk preview extraction failed: {ex.Message}");
            }
        }

        // ----------------------------------------------------------------- //
        // Chunking
        // ----------------------------------------------------------------- //
        private static List<ChunkPreviewItem> BuildChunks(List<(int? PageNumber, string Text)> sections)
        {
            var items = new List<ChunkPreviewItem>();
            var chunkIndex = 0;

            foreach (var (pageNumber, rawText) in sections)
            {
                var text = NormalizeWhitespace(rawText);

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var start = 0;

                while (start < text.Length)
                {
                    var hardEnd = Math.Min(start + ChunkSize, text.Length);
                    var end = hardEnd < text.Length
                        ? FindWordBreak(text, start, hardEnd)
                        : hardEnd;

                    if (end <= start)
                    {
                        end = hardEnd;
                    }

                    var chunkText = text.Substring(start, end - start).Trim();

                    if (chunkText.Length > 0)
                    {
                        items.Add(new ChunkPreviewItem
                        {
                            ChunkIndex = chunkIndex++,
                            PageNumber = pageNumber,
                            ChunkText = chunkText,
                            CharCount = chunkText.Length,
                            TokenEstimate = EstimateTokens(chunkText)
                        });
                    }

                    if (end >= text.Length)
                    {
                        break;
                    }

                    start = end - Overlap;
                    if (start < 0)
                    {
                        start = 0;
                    }

                    // Avoid starting the next window in the middle of a word.
                    while (start < end && start < text.Length && char.IsWhiteSpace(text[start]))
                    {
                        start++;
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Prefer splitting before whitespace so Vietnamese/English words stay intact.
        /// </summary>
        private static int FindWordBreak(string text, int start, int hardEnd)
        {
            for (var i = hardEnd - 1; i > start; i--)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }

            return hardEnd;
        }

        // Rough token estimate (~4 characters per token).
        private static int EstimateTokens(string text) =>
            (int)Math.Ceiling(text.Length / 4.0);

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Collapse Windows newlines but keep paragraph breaks readable.
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        // ----------------------------------------------------------------- //
        // Extraction per file type
        // ----------------------------------------------------------------- //
        private static List<(int?, string)> ExtractTxt(string filePath)
        {
            var text = File.ReadAllText(filePath);
            return new List<(int?, string)> { (null, text) };
        }

        private static List<(int?, string)> ExtractPdf(string filePath)
        {
            var sections = new List<(int?, string)>();

            using var pdf = PdfDocument.Open(filePath);

            foreach (var page in pdf.GetPages())
            {
                var pageText = page.Text ?? string.Empty;
                sections.Add((page.Number, pageText));
            }

            return sections;
        }

        private static List<(int?, string)> ExtractDocx(string filePath)
        {
            var builder = new StringBuilder();

            using var doc = WordprocessingDocument.Open(filePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;

            if (body != null)
            {
                foreach (var paragraph in body.Descendants<W.Paragraph>())
                {
                    var paragraphText = paragraph.InnerText;

                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        builder.AppendLine(paragraphText);
                    }
                }
            }

            return new List<(int?, string)> { (null, builder.ToString()) };
        }

        private static List<(int?, string)> ExtractPptx(string filePath)
        {
            var sections = new List<(int?, string)>();

            using var presentation = PresentationDocument.Open(filePath, false);
            var presentationPart = presentation.PresentationPart;

            if (presentationPart?.Presentation?.SlideIdList == null)
            {
                return sections;
            }

            var slideNumber = 0;

            foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
            {
                slideNumber++;

                var relationshipId = slideId.RelationshipId?.Value;

                if (string.IsNullOrEmpty(relationshipId))
                {
                    continue;
                }

                if (presentationPart.GetPartById(relationshipId) is not SlidePart slidePart)
                {
                    continue;
                }

                var builder = new StringBuilder();

                foreach (var text in slidePart.Slide.Descendants<A.Text>())
                {
                    if (!string.IsNullOrWhiteSpace(text.Text))
                    {
                        builder.AppendLine(text.Text);
                    }
                }

                sections.Add((slideNumber, builder.ToString()));
            }

            return sections;
        }
    }
}
