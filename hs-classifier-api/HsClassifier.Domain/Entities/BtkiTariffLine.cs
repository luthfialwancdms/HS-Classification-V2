using HsClassifier.Domain.Enums;
namespace HsClassifier.Domain.Entities;

public class BtkiTariffLine
{
    public long Id { get; set; }
    public Guid VersionId { get; set; }
    public string Code { get; set; } = string.Empty;         // "8471.30.20"
    public string? ParentCode { get; set; }                   // "8471.30"
    public string? SectionCode { get; set; }
    public string? ChapterCode { get; set; }
    public string? HeadingCode { get; set; }
    public string? SubheadingCode { get; set; }
    public int IndentLevel { get; set; }
    public string DescriptionId { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public string? Unit { get; set; }
    public IngestionMethod IngestionMethod { get; set; } = IngestionMethod.DocxParser;
    public string? SourceId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public NomenclatureVersion? Version { get; set; }
    public ICollection<MfnRate> MfnRates { get; set; } = new List<MfnRate>();
    public ICollection<FtaRate> FtaRates { get; set; } = new List<FtaRate>();
    public ICollection<TaxRate> TaxRates { get; set; } = new List<TaxRate>();
}
