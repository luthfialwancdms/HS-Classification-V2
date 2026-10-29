using HsClassifier.Domain.Enums;
namespace HsClassifier.Domain.Entities;

public class MfnRate
{
    public long Id { get; set; }
    public long TariffLineId { get; set; }
    public Guid VersionId { get; set; }
    public decimal RateValue { get; set; }
    public RateUnit RateUnit { get; set; } = RateUnit.Percent;
    public string? Currency { get; set; }
    public string? QuantityUnit { get; set; }
    public string? Formula { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? SourceId { get; set; }
    public bool IsActive { get; set; } = true;

    public BtkiTariffLine? TariffLine { get; set; }
}

public class FtaRate
{
    public long Id { get; set; }
    public long TariffLineId { get; set; }
    public Guid VersionId { get; set; }
    public string SchemeCode { get; set; } = string.Empty;
    public string PartnerCountryCode { get; set; } = string.Empty;
    public decimal RateValue { get; set; }
    public RateUnit RateUnit { get; set; } = RateUnit.Percent;
    public string? ConditionsSummary { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? SourceId { get; set; }
    public bool IsActive { get; set; } = true;

    public BtkiTariffLine? TariffLine { get; set; }
}

public class TaxRate
{
    public long Id { get; set; }
    public long TariffLineId { get; set; }
    public Guid VersionId { get; set; }
    public TaxType TaxType { get; set; }
    public decimal RateValue { get; set; }
    public RateUnit RateUnit { get; set; } = RateUnit.Percent;
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? SourceId { get; set; }
    public bool IsActive { get; set; } = true;

    public BtkiTariffLine? TariffLine { get; set; }
}
