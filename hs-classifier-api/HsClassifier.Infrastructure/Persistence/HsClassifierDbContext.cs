using HsClassifier.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Persistence;

public class HsClassifierDbContext : DbContext
{
    public HsClassifierDbContext(DbContextOptions<HsClassifierDbContext> options) : base(options) { }

    public DbSet<NomenclatureVersion> NomenclatureVersions => Set<NomenclatureVersion>();
    public DbSet<BtkiTariffLine> BtkiTariffLines => Set<BtkiTariffLine>();
    public DbSet<MfnRate> MfnRates => Set<MfnRate>();
    public DbSet<FtaRate> FtaRates => Set<FtaRate>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<ExplanatoryNote> ExplanatoryNotes => Set<ExplanatoryNote>();
    public DbSet<SectionNote> SectionNotes => Set<SectionNote>();
    public DbSet<ChapterNote> ChapterNotes => Set<ChapterNote>();
    public DbSet<SubheadingNote> SubheadingNotes => Set<SubheadingNote>();
    public DbSet<ClassificationCase> ClassificationCases => Set<ClassificationCase>();
    public DbSet<ReasoningEvent> ReasoningEvents => Set<ReasoningEvent>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<ExpertCorrection> ExpertCorrections => Set<ExpertCorrection>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<AmbiguityItem> AmbiguityItems => Set<AmbiguityItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // NomenclatureVersion
        modelBuilder.Entity<NomenclatureVersion>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.HsEdition).HasMaxLength(20);
            e.Property(x => x.BtkiEdition).HasMaxLength(50);
            e.Property(x => x.RegulationNumber).HasMaxLength(100);
            e.Property(x => x.State).HasConversion<string>();
        });

        // BtkiTariffLine
        modelBuilder.Entity<BtkiTariffLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(15).IsRequired();
            e.Property(x => x.ParentCode).HasMaxLength(15);
            e.Property(x => x.SectionCode).HasMaxLength(10);
            e.Property(x => x.ChapterCode).HasMaxLength(10);
            e.Property(x => x.HeadingCode).HasMaxLength(10);
            e.Property(x => x.SubheadingCode).HasMaxLength(15);
            e.Property(x => x.DescriptionId).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(50);
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.Property(x => x.IngestionMethod).HasConversion<string>();

            e.HasIndex(x => new { x.Code, x.VersionId });
            e.HasIndex(x => x.ParentCode);
            e.HasIndex(x => x.ChapterCode);
            e.HasIndex(x => new { x.IsActive, x.VersionId });

            e.HasOne(x => x.Version)
             .WithMany(v => v.TariffLines)
             .HasForeignKey(x => x.VersionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // MfnRate
        modelBuilder.Entity<MfnRate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RateValue).HasPrecision(10, 4);
            e.Property(x => x.RateUnit).HasConversion<string>();
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.HasIndex(x => new { x.TariffLineId, x.VersionId, x.IsActive });
            e.HasOne(x => x.TariffLine)
             .WithMany(t => t.MfnRates)
             .HasForeignKey(x => x.TariffLineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // FtaRate
        modelBuilder.Entity<FtaRate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RateValue).HasPrecision(10, 4);
            e.Property(x => x.RateUnit).HasConversion<string>();
            e.Property(x => x.SchemeCode).HasMaxLength(30);
            e.Property(x => x.PartnerCountryCode).HasMaxLength(10);
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.HasIndex(x => new { x.TariffLineId, x.SchemeCode });
            e.HasOne(x => x.TariffLine)
             .WithMany(t => t.FtaRates)
             .HasForeignKey(x => x.TariffLineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // TaxRate
        modelBuilder.Entity<TaxRate>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RateValue).HasPrecision(10, 4);
            e.Property(x => x.RateUnit).HasConversion<string>();
            e.Property(x => x.TaxType).HasConversion<string>();
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.HasIndex(x => new { x.TariffLineId, x.TaxType, x.IsActive });
            e.HasOne(x => x.TariffLine)
             .WithMany(t => t.TaxRates)
             .HasForeignKey(x => x.TariffLineId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ExplanatoryNote
        modelBuilder.Entity<ExplanatoryNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.HeadingCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.SubheadingCode).HasMaxLength(15);
            e.Property(x => x.ParagraphType).HasConversion<string>();
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.HasIndex(x => new { x.HeadingCode, x.VersionId });
            e.HasIndex(x => x.ParagraphType);
        });

        // SectionNote
        modelBuilder.Entity<SectionNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SectionNumber).HasMaxLength(10).IsRequired();
            e.Property(x => x.NoteNumber).HasMaxLength(20);
            e.Property(x => x.EffectType).HasConversion<string>();
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.HasIndex(x => new { x.SectionNumber, x.VersionId });
        });

        // ChapterNote
        modelBuilder.Entity<ChapterNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ChapterNumber).HasMaxLength(10).IsRequired();
            e.Property(x => x.TargetHeading).HasMaxLength(10);
            e.Property(x => x.NoteNumber).HasMaxLength(20);
            e.Property(x => x.EffectType).HasConversion<string>();
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.HasIndex(x => new { x.ChapterNumber, x.VersionId });
            e.HasIndex(x => x.TargetHeading);
        });

        // SubheadingNote
        modelBuilder.Entity<SubheadingNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.HeadingCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.NoteNumber).HasMaxLength(20);
            e.Property(x => x.EffectType).HasConversion<string>();
            e.Property(x => x.SourceId).HasMaxLength(500);
            e.HasIndex(x => new { x.HeadingCode, x.VersionId });
        });

        // ClassificationCase
        modelBuilder.Entity<ClassificationCase>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FactsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CandidateCodesJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.ConfidenceJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.FinalCode).HasMaxLength(15);
            e.Property(x => x.Confidence).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => x.FinalCode);
        });

        // ReasoningEvent
        modelBuilder.Entity<ReasoningEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RuleApplied).HasMaxLength(50);
            e.Property(x => x.Decision).HasMaxLength(50);
            e.Property(x => x.FactsUsedJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CandidatesBeforeJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CandidatesAfterJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.Reasoning).HasColumnType("nvarchar(max)");
            e.Property(x => x.SourceIdsJson).HasColumnType("nvarchar(max)");
            e.HasIndex(x => new { x.CaseId, x.EventOrder });
            e.HasOne(x => x.Case)
             .WithMany(c => c.ReasoningEvents)
             .HasForeignKey(x => x.CaseId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // AppUser
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasConversion<string>();
            e.HasIndex(x => x.Email).IsUnique();
        });

        // ExpertCorrection — append-only audit of expert decisions
        modelBuilder.Entity<ExpertCorrection>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(30).IsRequired();
            e.Property(x => x.BeforeCode).HasMaxLength(15);
            e.Property(x => x.AfterCode).HasMaxLength(15);
            e.Property(x => x.BeforeStatus).HasMaxLength(30);
            e.Property(x => x.AfterStatus).HasMaxLength(30);
            e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => x.CaseId);
        });

        // ImportBatch — persisted validation report per upload
        modelBuilder.Entity<ImportBatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(300);
            e.Property(x => x.Checksum).HasMaxLength(100);
            e.Property(x => x.ReportJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedBy).HasMaxLength(200);
            e.HasIndex(x => x.VersionId);
        });

        // AmbiguityItem — STRUCTURE_AMBIGUITY review queue
        modelBuilder.Entity<AmbiguityItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(15).IsRequired();
            e.Property(x => x.ProposedParent).HasMaxLength(15);
            e.Property(x => x.CorrectedParent).HasMaxLength(15);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Note).HasMaxLength(2000);
            e.Property(x => x.DecidedBy).HasMaxLength(200);
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.BatchId, x.Decision });
            e.HasOne(x => x.Batch).WithMany(b => b.Items)
             .HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
