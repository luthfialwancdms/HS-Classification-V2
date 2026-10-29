using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HsClassifier.Application.Interfaces;

namespace HsClassifier.Infrastructure.Parsing;

/// <summary>
/// Parses BTKI DOCX, Explanatory Notes DOCX, and Legal Notes DOCX using OpenXML SDK.
/// ponytail: single-pass streaming per document type, no intermediate DOM tree.
/// </summary>
public class DocxParser : IDocxParser
{
    // ─── Shared helpers ───────────────────────────────────────────────────────

    private static string ComputeChecksum(Stream stream)
    {
        stream.Position = 0;
        var hash = SHA256.HashData(stream);
        stream.Position = 0;
        return Convert.ToHexString(hash).ToLower()[..12];
    }

    private static string FormatCode(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 8 && raw.All(char.IsDigit))
            return $"{raw[..4]}.{raw[4..6]}.{raw[6..]}";
        return raw;
    }

    private static string? DeriveParent(string code)
    {
        // "0101.21.00" → "0101.21" if 6-digit exists, else "0101"
        var parts = code.Split('.');
        if (parts.Length == 3) return $"{parts[0]}.{parts[1]}";
        if (parts.Length == 2) return parts[0];
        return null;
    }

    private static List<string> ExtractHsCodes(string text)
    {
        // Match patterns like "84.71", "8471", "8471.30"
        var codes = new List<string>();
        foreach (Match m in Regex.Matches(text, @"\b(\d{2})\.(\d{2})(?:\.(\d{2}))?\b"))
            codes.Add(m.Value.Replace(".", "")[..4]);
        foreach (Match m in Regex.Matches(text, @"\b(\d{4})\b"))
            if (!codes.Contains(m.Value)) codes.Add(m.Value);
        return codes.Distinct().ToList();
    }

    private static string ClassifyParagraphType(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("tidak termasuk") || t.Contains("does not cover") || t.Contains("excluded") || t.Contains("dikecualikan"))
            return "EXCLUSION";
        if (t.Contains("termasuk dalam") || t.Contains("this heading includes") || t.Contains("meliputi"))
            return "INCLUSION";
        if (t.Contains("berarti") || t.Contains("means ") || t.Contains("refers to") || t.Contains("didefinisikan"))
            return "DEFINITION";
        if (t.Contains("see also") || t.Contains("lihat juga") || t.Contains("heading") && t.Contains("refer"))
            return "CROSS_REFERENCE";
        if (t.StartsWith("contoh") || t.StartsWith("example"))
            return "EXAMPLE";
        if (t.Contains("karakteristik") || t.Contains("technical characteristic"))
            return "TECHNICAL";
        return "SCOPE";
    }

    private static string ClassifyEffectType(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("tidak termasuk") || t.Contains("does not include") || t.Contains("dikecualikan") || t.Contains("exclud"))
            return "EXCLUSION";
        if (t.Contains("meliputi") || t.Contains("termasuk") || t.Contains("includes") || t.Contains("cover"))
            return "INCLUSION";
        if (t.Contains("dengan syarat") || t.Contains("provided that") || t.Contains("with the condition"))
            return "CONDITION";
        if (t.Contains("diklasifikasikan") || t.Contains("classified") || t.Contains("lihat") || t.Contains("see heading"))
            return "REFERRAL";
        if (t.Contains("prioritas") || t.Contains("priority") || t.Contains("prevail"))
            return "PRIORITY";
        return "DEFINITION";
    }

    // ─── Task 2.1: BTKI Parser ────────────────────────────────────────────────

    public async Task<BtkiParseResult> ParseBTKIAsync(Stream docxStream, string fileName)
    {
        var checksum = ComputeChecksum(docxStream);
        var result = new BtkiParseResult { SourceFileName = fileName, SourceChecksum = checksum };
        var seenCodes = new HashSet<string>();
        var row = 0;

        using var doc = WordprocessingDocument.Open(docxStream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) { result.Issues.Add(new ParseIssue { Severity = "ERROR", Type = "PARSE_FAILED", Message = "Document body is null." }); return result; }

        // Process tables (tariff rates are usually in tables)
        foreach (var table in body.Descendants<Table>())
        {
            foreach (var tableRow in table.Descendants<TableRow>())
            {
                row++;
                var cells = tableRow.Descendants<TableCell>().ToList();
                if (cells.Count < 2) { result.SkippedRows++; continue; }

                var cell0 = cells[0].InnerText.Trim();
                // Try to find an 8-digit code
                var codeMatch = Regex.Match(cell0, @"\d{8}");
                if (!codeMatch.Success && cells.Count > 1)
                    codeMatch = Regex.Match(cells[1].InnerText.Trim(), @"\d{8}");
                if (!codeMatch.Success) { result.SkippedRows++; continue; }

                var rawCode = codeMatch.Value;
                var formatted = FormatCode(rawCode);

                // Duplicate check
                if (seenCodes.Contains(formatted))
                {
                    result.Issues.Add(new ParseIssue { Severity = "WARNING", Type = "DUPLICATE_CODE", Code = formatted, Message = $"Duplicate code {formatted}", RowNumber = row });
                    result.SkippedRows++;
                    continue;
                }
                seenCodes.Add(formatted);

                var descId = cells.Count > 1 ? cells[1].InnerText.Trim() : string.Empty;
                var descEn = cells.Count > 2 ? cells[2].InnerText.Trim() : null;

                // Try parse MFN rate
                decimal? mfnRate = null;
                string mfnUnit = "PERCENT";
                if (cells.Count > 3)
                {
                    var rateText = cells[3].InnerText.Trim().Replace("%", "").Replace(",", ".");
                    if (decimal.TryParse(rateText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                        mfnRate = parsed;
                }

                var node = new ParsedTariffNode
                {
                    Code = formatted,
                    ParentCode = DeriveParent(formatted),
                    ChapterCode = rawCode[..2],
                    HeadingCode = rawCode[..4],
                    SubheadingCode = $"{rawCode[..4]}.{rawCode[4..6]}",
                    IndentLevel = 3,
                    DescriptionId = descId,
                    DescriptionEn = string.IsNullOrEmpty(descEn) ? null : descEn,
                    MfnRate = mfnRate,
                    MfnRateUnit = mfnUnit,
                    SourceId = $"docx:{checksum}:table:row_{row}",
                    Status = "A",
                };

                result.TariffNodes.Add(node);
            }
        }

        // Also process paragraphs for codes not in tables
        foreach (var para in body.Descendants<Paragraph>())
        {
            row++;
            var text = para.InnerText.Trim();
            var codeMatch = Regex.Match(text, @"^\s*(\d{8})\s+(.+)$");
            if (!codeMatch.Success) continue;

            var rawCode = codeMatch.Groups[1].Value;
            var formatted = FormatCode(rawCode);
            if (seenCodes.Contains(formatted)) continue;
            seenCodes.Add(formatted);

            var descId = codeMatch.Groups[2].Value.Trim();

            // Derive indent from paragraph indent settings
            var indentPr = para.ParagraphProperties?.Indentation;
            var indentVal = int.TryParse(indentPr?.Left?.Value, out var iv) ? iv : 0;
            var indentLevel = indentVal switch { > 2000 => 4, > 1400 => 3, > 700 => 2, _ => 1 };

            result.TariffNodes.Add(new ParsedTariffNode
            {
                Code = formatted,
                ParentCode = DeriveParent(formatted),
                ChapterCode = rawCode[..2],
                HeadingCode = rawCode[..4],
                IndentLevel = indentLevel,
                DescriptionId = descId,
                SourceId = $"docx:{checksum}:para:row_{row}",
                Status = "A",
            });
        }

        result.TotalRows = row;
        return await Task.FromResult(result);
    }

    // ─── Task 3.1: EN Parser ──────────────────────────────────────────────────

    public async Task<EnParseResult> ParseENAsync(Stream docxStream, string fileName, string edition = "2022")
    {
        var checksum = ComputeChecksum(docxStream);
        var result = new EnParseResult { SourceFileName = fileName, SourceChecksum = checksum };

        using var doc = WordprocessingDocument.Open(docxStream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return result;

        var currentHeading = "0000";
        var seqInHeading = 0;
        var paraIndex = 0;

        foreach (var para in body.Descendants<Paragraph>())
        {
            paraIndex++;
            var text = para.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Detect heading changes — bold paragraphs starting with heading code
            var headingMatch = Regex.Match(text, @"^(\d{4})\b");
            var isBold = para.Descendants<Bold>().Any();
            if (headingMatch.Success && isBold)
            {
                currentHeading = headingMatch.Groups[1].Value;
                seqInHeading = 0;
                continue;
            }

            // Skip very short lines unlikely to be EN content
            if (text.Length < 15) continue;

            seqInHeading++;
            var paraType = ClassifyParagraphType(text);
            var referred = ExtractHsCodes(text);

            result.Paragraphs.Add(new ParsedEnParagraph
            {
                HeadingCode = currentHeading,
                ParagraphType = paraType,
                TextContent = text,
                ReferredCodesJson = referred.Count > 0 ? JsonSerializer.Serialize(referred) : null,
                SourceId = $"docx:{checksum}:en:{currentHeading}:para_{paraIndex}",
                SequenceInHeading = seqInHeading,
            });
        }

        result.TotalParagraphs = result.Paragraphs.Count;
        return await Task.FromResult(result);
    }

    // ─── Task 4.1: Legal Notes Parser ────────────────────────────────────────

    public async Task<LegalNotesParseResult> ParseLegalNotesAsync(Stream docxStream, string fileName)
    {
        var checksum = ComputeChecksum(docxStream);
        var result = new LegalNotesParseResult { SourceFileName = fileName, SourceChecksum = checksum };

        using var doc = WordprocessingDocument.Open(docxStream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return result;

        var currentType = "CHAPTER";
        var currentScope = "00";
        var noteNumber = 0;
        var paraIndex = 0;

        foreach (var para in body.Descendants<Paragraph>())
        {
            paraIndex++;
            var text = para.InnerText.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var upper = text.ToUpperInvariant();

            // Detect note section type
            if (upper.Contains("CATATAN BAGIAN") || upper.Contains("SECTION NOTE"))
            { currentType = "SECTION"; noteNumber = 0; continue; }
            if (upper.Contains("CATATAN BAB") || upper.Contains("CHAPTER NOTE"))
            { currentType = "CHAPTER"; noteNumber = 0; continue; }
            if (upper.Contains("CATATAN SUBPOS") || upper.Contains("SUBHEADING NOTE"))
            { currentType = "SUBHEADING"; noteNumber = 0; continue; }

            // Detect scope code (roman numeral section or chapter number)
            var romanMatch = Regex.Match(text, @"^(I{1,3}|IV|VI{0,3}|IX|XI{0,3}|XIV|XV|XVI|XVII|XVIII|XIX|XX{0,3}|XXI)\b");
            if (romanMatch.Success && text.Length < 30) { currentScope = romanMatch.Value; noteNumber = 0; continue; }
            var chapMatch = Regex.Match(text, @"^BAB\s+(\d+)|^CHAPTER\s+(\d+)", RegexOptions.IgnoreCase);
            if (chapMatch.Success) { currentScope = (chapMatch.Groups[1].Value + chapMatch.Groups[2].Value).Trim(); noteNumber = 0; continue; }

            // Skip very short or header-like lines
            if (text.Length < 20) continue;

            noteNumber++;
            var effectType = ClassifyEffectType(text);
            var referred = ExtractHsCodes(text);

            var note = new ParsedNote
            {
                ScopeCode = currentScope,
                NoteType = currentType,
                NoteNumber = noteNumber.ToString(),
                TextId = text,
                EffectType = effectType,
                ReferredCodesJson = referred.Count > 0 ? JsonSerializer.Serialize(referred) : null,
                SourceId = $"docx:{checksum}:notes:{currentType.ToLower()}:{currentScope}:para_{paraIndex}",
            };

            if (currentType == "SECTION") result.SectionNotes.Add(note);
            else if (currentType == "SUBHEADING") result.SubheadingNotes.Add(note);
            else result.ChapterNotes.Add(note);
        }

        return await Task.FromResult(result);
    }
}
