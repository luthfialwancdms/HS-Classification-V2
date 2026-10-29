using HsClassifier.Domain.Enums;
namespace HsClassifier.Domain.Entities;

public class NomenclatureVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string HsEdition { get; set; } = "2022";
    public string BtkiEdition { get; set; } = string.Empty;
    public string RegulationNumber { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public ImportState State { get; set; } = ImportState.Uploaded;
    public bool IsActive { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    public ICollection<BtkiTariffLine> TariffLines { get; set; } = new List<BtkiTariffLine>();
    public ICollection<ExplanatoryNote> ExplanatoryNotes { get; set; } = new List<ExplanatoryNote>();
    public ICollection<SectionNote> SectionNotes { get; set; } = new List<SectionNote>();
    public ICollection<ChapterNote> ChapterNotes { get; set; } = new List<ChapterNote>();
    public ICollection<SubheadingNote> SubheadingNotes { get; set; } = new List<SubheadingNote>();
}
