using System.Security.Cryptography;
using HsClassifier.Application.Interfaces;
using HsClassifier.Application.Services;
using HsClassifier.Domain.Enums;
using UglyToad.PdfPig;

namespace HsClassifier.Infrastructure.Parsing;

/// <summary>
/// Official BTKI PDF (repository.beacukai.go.id, "chapter-NN.pdf" or the merged book) → <see cref="BtkiParseResult"/>.
/// PdfPig only supplies words and positions; the row/hierarchy logic is in the testable
/// <see cref="BtkiTableAssembler"/> and <see cref="BtkiHierarchyBuilder"/>.
/// Pages with no text layer (scanned chapters 1–15) are reported, never guessed.
/// </summary>
public class BtkiPdfParser : IBtkiPdfParser
{
    public Task<BtkiParseResult> ParseAsync(Stream pdf, string fileName)
    {
        using var ms = new MemoryStream();
        pdf.CopyTo(ms);
        var bytes = ms.ToArray();

        var result = new BtkiParseResult
        {
            SourceFileName = fileName,
            SourceChecksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            IngestionMethod = IngestionMethod.PdfParser,
        };

        PdfDocument doc;
        try { doc = PdfDocument.Open(bytes); }
        catch (Exception ex)
        {
            result.Issues.Add(new ParseIssue { Severity = "ERROR", Type = "PARSE_FAILED", Message = $"PDF tidak dapat dibaca: {ex.Message}" });
            return Task.FromResult(result);
        }

        using (doc)
        {
            var pages = new List<PdfPageWords>();
            var noText = new List<int>();
            foreach (var p in doc.GetPages())
            {
                // True baseline and font size of the first letter (the glyph box varies with the letters in the word)
                var words = p.GetWords()
                    .Select(w => w.Letters.Count > 0
                        ? new PdfWordBox(w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.Letters[0].StartBaseLine.Y, (double)w.Letters[0].PointSize)
                        : new PdfWordBox(w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Bottom))
                    .ToList();
                if (words.Count < 5) noText.Add(p.Number);
                pages.Add(new PdfPageWords(p.Number, p.Width, p.Height, words));
            }

            if (noText.Count > 0)
                result.Issues.Add(new ParseIssue
                {
                    Severity = "INFO", Type = "NO_TEXT_PAGE", RowNumber = 0,
                    Message = $"{noText.Count} halaman tanpa teks (hasil scan gambar, perlu OCR; dilewati): " +
                              string.Join(", ", noText.Take(60)) + (noText.Count > 60 ? ", …" : ""),
                });

            var assembly = BtkiTableAssembler.Assemble(pages.Where(p => p.Words.Count >= 5), result.Issues);
            result.TotalRows = assembly.Rows.Count;
            result.SkippedRows = assembly.SkippedLines;
            result.TariffNodes = BtkiHierarchyBuilder.Build(assembly.Rows, fileName, result.Issues, result.MissingGroupCodes);

            if (result.TariffNodes.Count == 0)
                result.Issues.Add(new ParseIssue
                {
                    Severity = "ERROR", Type = "NO_TARIFF_ROWS", Message = "Tidak ada baris tarif yang terbaca dari PDF ini.",
                });
        }
        return Task.FromResult(result);
    }

    public async Task<BtkiParseResult> ParseManyAsync(IReadOnlyList<(Stream Stream, string FileName)> files)
    {
        if (files.Count == 1) return await ParseAsync(files[0].Stream, files[0].FileName);

        var parts = new List<BtkiParseResult>();
        foreach (var f in files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase))
            parts.Add(await ParseAsync(f.Stream, f.FileName));

        var merged = new BtkiParseResult
        {
            SourceFileName = parts.Count == 0 ? "-" : $"{parts.Count} file PDF ({parts[0].SourceFileName} ... {parts[^1].SourceFileName})",
            // order-independent: the same set of files always gives the same checksum
            SourceChecksum = Convert.ToHexString(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(string.Join("|", parts.Select(p => p.SourceChecksum).Order(StringComparer.Ordinal))))).ToLowerInvariant(),
            IngestionMethod = IngestionMethod.PdfParser,
        };
        foreach (var p in parts)
        {
            merged.TariffNodes.AddRange(p.TariffNodes);
            merged.TotalRows += p.TotalRows;
            merged.SkippedRows += p.SkippedRows;
            merged.MissingGroupCodes.AddRange(p.MissingGroupCodes);
            foreach (var i in p.Issues)
            {
                i.Message = $"[{p.SourceFileName}] {i.Message}";
                merged.Issues.Add(i);
            }
        }
        merged.MissingGroupCodes = merged.MissingGroupCodes.Distinct().ToList();
        return merged;
    }
}
