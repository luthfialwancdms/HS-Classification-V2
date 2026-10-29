using HsClassifier.Domain.Enums;

namespace HsClassifier.Application.Interfaces;

// ─── Shared ───────────────────────────────────────────────────────────────────

public class ParseIssue
{
    public string Severity { get; set; } = "WARNING"; // ERROR | WARNING | INFO
    public string Type { get; set; } = string.Empty;  // DUPLICATE_CODE | MISSING_PARENT | STRUCTURE_AMBIGUITY …
    public string? Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RowNumber { get; set; }
}

// ─── BTKI ─────────────────────────────────────────────────────────────────────

public class ParsedTariffNode
{
    public string Code { get; set; } = string.Empty;          // "0101.21.00"
    public string? ParentCode { get; set; }
    public string? SectionCode { get; set; }
    public string? ChapterCode { get; set; }
    public string? HeadingCode { get; set; }
    public string? SubheadingCode { get; set; }
    public int IndentLevel { get; set; }
    public string DescriptionId { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public decimal? MfnRate { get; set; }
    public string MfnRateUnit { get; set; } = "PERCENT";
    public decimal? PpnRate { get; set; }
    public decimal? Pph22Rate { get; set; }
    public string Status { get; set; } = "A";
    public string SourceId { get; set; } = string.Empty;
    public bool IsAmbiguous { get; set; }
    public string? AmbiguityReason { get; set; }
}

public class BtkiParseResult
{
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceChecksum { get; set; } = string.Empty;
    public List<ParsedTariffNode> TariffNodes { get; set; } = new();
    public List<ParseIssue> Issues { get; set; } = new();
    public int TotalRows { get; set; }
    public int SkippedRows { get; set; }
    /// <summary>How the nodes were read; stored on every committed tariff line.</summary>
    public IngestionMethod IngestionMethod { get; set; } = IngestionMethod.DocxParser;
    /// <summary>6-char group codes ("7409.1") that some tariff lines hang under but whose text row is absent in the source.</summary>
    public List<string> MissingGroupCodes { get; set; } = new();
}

// ─── Explanatory Notes ────────────────────────────────────────────────────────

public class ParsedEnParagraph
{
    public string HeadingCode { get; set; } = string.Empty;
    public string? SubheadingCode { get; set; }
    public string? ParagraphNumber { get; set; }
    public string ParagraphType { get; set; } = "SCOPE";
    public string TextContent { get; set; } = string.Empty;
    public string? ReferredCodesJson { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public int SequenceInHeading { get; set; }
}

public class EnParseResult
{
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceChecksum { get; set; } = string.Empty;
    public List<ParsedEnParagraph> Paragraphs { get; set; } = new();
    public List<ParseIssue> Issues { get; set; } = new();
    public int TotalParagraphs { get; set; }
}

// ─── Legal Notes ──────────────────────────────────────────────────────────────

public class ParsedNote
{
    public string ScopeCode { get; set; } = string.Empty;  // "XVI", "84", "8471"
    public string NoteType { get; set; } = "CHAPTER";      // SECTION | CHAPTER | SUBHEADING
    public string? TargetHeading { get; set; }
    public string NoteNumber { get; set; } = string.Empty;
    public string? ParagraphNumber { get; set; }
    public string TextId { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string EffectType { get; set; } = "DEFINITION";
    public string? TargetCodesJson { get; set; }
    public string? ReferredCodesJson { get; set; }
    public string SourceId { get; set; } = string.Empty;
}

public class LegalNotesParseResult
{
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceChecksum { get; set; } = string.Empty;
    public List<ParsedNote> SectionNotes { get; set; } = new();
    public List<ParsedNote> ChapterNotes { get; set; } = new();
    public List<ParsedNote> SubheadingNotes { get; set; } = new();
    public List<ParseIssue> Issues { get; set; } = new();
}

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IDocxParser
{
    Task<BtkiParseResult> ParseBTKIAsync(Stream docxStream, string fileName);
    Task<EnParseResult> ParseENAsync(Stream docxStream, string fileName, string edition = "2022");
    Task<LegalNotesParseResult> ParseLegalNotesAsync(Stream docxStream, string fileName);
}
