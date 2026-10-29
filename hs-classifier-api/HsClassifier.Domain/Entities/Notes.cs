using HsClassifier.Domain.Enums;
namespace HsClassifier.Domain.Entities;

public class ExplanatoryNote
{
    public long Id { get; set; }
    public Guid VersionId { get; set; }
    public string HeadingCode { get; set; } = string.Empty;
    public string? SubheadingCode { get; set; }
    public string? ParagraphNumber { get; set; }
    public ParagraphType ParagraphType { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public long? PreviousParagraphId { get; set; }
    public long? NextParagraphId { get; set; }
    public long? ParentSectionId { get; set; }
    public string? ReferredCodesJson { get; set; }   // JSON array
    public string? SourceId { get; set; }
}

public class SectionNote
{
    public int Id { get; set; }
    public Guid VersionId { get; set; }
    public string SectionNumber { get; set; } = string.Empty;
    public string NoteNumber { get; set; } = string.Empty;
    public string? ParagraphNumber { get; set; }
    public string TextId { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public EffectType EffectType { get; set; }
    public string? TargetCodesJson { get; set; }
    public string? ReferredCodesJson { get; set; }
    public string? SourceId { get; set; }
}

public class ChapterNote
{
    public int Id { get; set; }
    public Guid VersionId { get; set; }
    public string ChapterNumber { get; set; } = string.Empty;
    public string? TargetHeading { get; set; }
    public string NoteNumber { get; set; } = string.Empty;
    public string? ParagraphNumber { get; set; }
    public string TextId { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public EffectType EffectType { get; set; }
    public string? TargetCodesJson { get; set; }
    public string? ReferredCodesJson { get; set; }
    public string? SourceId { get; set; }
}

public class SubheadingNote
{
    public int Id { get; set; }
    public Guid VersionId { get; set; }
    public string HeadingCode { get; set; } = string.Empty;
    public string NoteNumber { get; set; } = string.Empty;
    public string TextId { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public EffectType EffectType { get; set; }
    public string? TargetCodesJson { get; set; }
    public string? ReferredCodesJson { get; set; }
    public string? SourceId { get; set; }
}
