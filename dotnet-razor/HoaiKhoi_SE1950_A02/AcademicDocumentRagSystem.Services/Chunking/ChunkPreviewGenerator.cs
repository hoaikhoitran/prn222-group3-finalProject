using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using UglyToad.PdfPig;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocumentRagSystem.Services.Chunking
{
    public class ChunkPreviewGenerator : IChunkPreviewGenerator
    {
        private const string ScanOnlyMessage =
            "Khong trich xuat duoc text tu tai lieu nay. Tai lieu co the la ban scan hoac anh.";

        public ChunkPreviewResult Generate(string filePath, string fileType)
        {
            return Generate(filePath, fileType, ChunkPreviewOptions.Default);
        }

        public ChunkPreviewResult Generate(string filePath, string fileType, ChunkPreviewOptions options)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return ChunkPreviewResult.Fail("Saved file was not found for chunk preview generation.");
            }

            var extension = (fileType ?? Path.GetExtension(filePath)).Trim().ToLowerInvariant();

            try
            {
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
                    return ChunkPreviewResult.Fail(ScanOnlyMessage);
                }

                var items = BuildChunks(sections, NormalizeOptions(options));

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

        public ChunkPreviewResult GenerateFromText(string text, ChunkPreviewOptions options)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return ChunkPreviewResult.Fail("Sample text is required for preview.");
            }

            var items = BuildChunks(
                new List<(int? PageNumber, string Text)> { (null, text) },
                NormalizeOptions(options));

            if (items.Count == 0)
            {
                return ChunkPreviewResult.Fail("No chunks were produced. Try lowering the minimum chunk length.");
            }

            return ChunkPreviewResult.Ok(items);
        }

        private static List<ChunkPreviewItem> BuildChunks(
            List<(int? PageNumber, string Text)> sections,
            ChunkPreviewOptions options)
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

                var chunks = options.ChunkMode switch
                {
                    "Words" => SplitByWords(text, options.ChunkSize, options.ChunkOverlap),
                    "Paragraph" => SplitByParagraphs(text, options.ChunkSize, options.ChunkOverlap),
                    _ => SplitByCharacters(text, options.ChunkSize, options.ChunkOverlap)
                };

                foreach (var chunkText in chunks)
                {
                    if (chunkText.Length < options.MinChunkLength)
                    {
                        continue;
                    }

                    items.Add(new ChunkPreviewItem
                    {
                        ChunkIndex = chunkIndex++,
                        PageNumber = pageNumber,
                        ChunkText = chunkText,
                        CharCount = chunkText.Length,
                        TokenEstimate = EstimateTokens(chunkText)
                    });

                    if (items.Count >= options.MaxPreviewChunks)
                    {
                        return items;
                    }
                }
            }

            return items;
        }

        private static List<string> SplitByCharacters(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            var start = 0;

            while (start < text.Length)
            {
                var hardEnd = Math.Min(start + chunkSize, text.Length);
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
                    chunks.Add(chunkText);
                }

                if (end >= text.Length)
                {
                    break;
                }

                start = Math.Max(0, end - overlap);
                while (start < end && start < text.Length && char.IsWhiteSpace(text[start]))
                {
                    start++;
                }
            }

            return chunks;
        }

        private static List<string> SplitByWords(string text, int chunkSize, int overlap)
        {
            var words = Regex.Matches(text, @"\S+")
                .Select(m => m.Value)
                .ToList();

            if (words.Count == 0)
            {
                return new List<string>();
            }

            var chunks = new List<string>();
            var stride = Math.Max(1, chunkSize - overlap);

            for (var start = 0; start < words.Count; start += stride)
            {
                var count = Math.Min(chunkSize, words.Count - start);
                chunks.Add(string.Join(" ", words.Skip(start).Take(count)).Trim());

                if (start + count >= words.Count)
                {
                    break;
                }
            }

            return chunks.Where(c => c.Length > 0).ToList();
        }

        private static List<string> SplitByParagraphs(string text, int chunkSize, int overlap)
        {
            var paragraphs = Regex.Split(text, @"(?:\n\s*){2,}")
                .Select(p => NormalizeWhitespace(p))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (paragraphs.Count <= 1)
            {
                paragraphs = text.Split('\n')
                    .Select(p => NormalizeWhitespace(p))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
            }

            if (paragraphs.Count == 0)
            {
                return new List<string>();
            }

            var chunks = new List<string>();
            var stride = Math.Max(1, chunkSize - overlap);

            for (var start = 0; start < paragraphs.Count; start += stride)
            {
                var count = Math.Min(chunkSize, paragraphs.Count - start);
                chunks.Add(string.Join("\n\n", paragraphs.Skip(start).Take(count)).Trim());

                if (start + count >= paragraphs.Count)
                {
                    break;
                }
            }

            return chunks.Where(c => c.Length > 0).ToList();
        }

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

        private static int EstimateTokens(string text) =>
            (int)Math.Ceiling(text.Length / 4.0);

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private static ChunkPreviewOptions NormalizeOptions(ChunkPreviewOptions? options)
        {
            var source = options ?? ChunkPreviewOptions.Default;
            var size = Math.Max(1, source.ChunkSize);
            var overlap = Math.Max(0, source.ChunkOverlap);

            if (overlap >= size)
            {
                overlap = Math.Max(0, size / 4);
            }

            return new ChunkPreviewOptions
            {
                ChunkMode = source.ChunkMode switch
                {
                    "Words" => "Words",
                    "Paragraph" => "Paragraph",
                    _ => "Characters"
                },
                ChunkSize = size,
                ChunkOverlap = overlap,
                MinChunkLength = Math.Max(0, source.MinChunkLength),
                MaxPreviewChunks = Math.Clamp(source.MaxPreviewChunks, 1, 10000)
            };
        }

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
