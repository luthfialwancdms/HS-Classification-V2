using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Copies a version (tariff lines, MFN/FTA/tax rates, legal notes, Explanatory Notes) into a new draft that is Indexed
/// and not active. Notes and EN are loaded into the draft, compared with the active version, and only then activated.
/// Ids are remapped by entity, not by code, because the source CSV has duplicate codes (e.g. 8504.50.20).
/// </summary>
public class VersionCloneService(HsClassifierDbContext db)
{
    private const int Chunk = 1000;

    /// <param name="sourceId">Version to copy; null = the active version.</param>
    public async Task<(NomenclatureVersion? Clone, string? Error)> CloneAsync(
        Guid? sourceId, string? name, string createdBy, CancellationToken ct = default)
    {
        var source = sourceId is null
            ? await db.NomenclatureVersions.AsNoTracking().FirstOrDefaultAsync(v => v.IsActive, ct)
            : await db.NomenclatureVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == sourceId, ct);
        if (source is null) return (null, sourceId is null ? "Tidak ada versi aktif untuk diklon." : "Versi tidak ditemukan.");
        if (!await db.BtkiTariffLines.AnyAsync(t => t.VersionId == source.Id, ct))
            return (null, "Versi sumber belum berisi baris tarif BTKI.");

        var clone = new NomenclatureVersion
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{source.Name} (draft {DateTime.UtcNow:yyyy-MM-dd HH:mm})" : name.Trim(),
            Description = source.Description, HsEdition = source.HsEdition, BtkiEdition = source.BtkiEdition,
            RegulationNumber = source.RegulationNumber, EffectiveFrom = source.EffectiveFrom, EffectiveTo = source.EffectiveTo,
            State = ImportState.Indexed, IsActive = false, CreatedBy = createdBy,
        };
        db.NomenclatureVersions.Add(clone);
        await db.SaveChangesAsync(ct);

        try
        {
            await CopyAsync(source.Id, clone.Id, ct);
        }
        catch
        {
            // ponytail: no transaction (EF InMemory has none); a failed copy is removed by hand so no half draft stays
            await DeleteAsync(clone.Id);
            throw;
        }
        return (clone, null);
    }

    private async Task CopyAsync(Guid from, Guid to, CancellationToken ct)
    {
        // Tariff lines, keeping the old id -> new id map for the rates
        var lineMap = new Dictionary<long, long>();
        var lines = await db.BtkiTariffLines.AsNoTracking().Where(t => t.VersionId == from).OrderBy(t => t.Id).ToListAsync(ct);
        foreach (var chunk in lines.Chunk(Chunk))
        {
            var copies = chunk.Select(t => new BtkiTariffLine
            {
                VersionId = to, Code = t.Code, ParentCode = t.ParentCode, SectionCode = t.SectionCode, ChapterCode = t.ChapterCode,
                HeadingCode = t.HeadingCode, SubheadingCode = t.SubheadingCode, IndentLevel = t.IndentLevel,
                DescriptionId = t.DescriptionId, DescriptionEn = t.DescriptionEn, Unit = t.Unit,
                IngestionMethod = t.IngestionMethod, SourceId = t.SourceId, IsActive = t.IsActive,
                EffectiveFrom = t.EffectiveFrom, EffectiveTo = t.EffectiveTo,
            }).ToList();
            db.BtkiTariffLines.AddRange(copies);
            await db.SaveChangesAsync(ct);
            for (var i = 0; i < chunk.Length; i++) lineMap[chunk[i].Id] = copies[i].Id;
            db.ChangeTracker.Clear();
        }

        foreach (var chunk in (await db.MfnRates.AsNoTracking().Where(r => r.VersionId == from).ToListAsync(ct)).Chunk(Chunk))
        {
            db.MfnRates.AddRange(chunk.Where(r => lineMap.ContainsKey(r.TariffLineId)).Select(r => new MfnRate
            {
                TariffLineId = lineMap[r.TariffLineId], VersionId = to, RateValue = r.RateValue, RateUnit = r.RateUnit,
                Currency = r.Currency, QuantityUnit = r.QuantityUnit, Formula = r.Formula,
                EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, SourceId = r.SourceId, IsActive = r.IsActive,
            }));
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        foreach (var chunk in (await db.FtaRates.AsNoTracking().Where(r => r.VersionId == from).ToListAsync(ct)).Chunk(Chunk))
        {
            db.FtaRates.AddRange(chunk.Where(r => lineMap.ContainsKey(r.TariffLineId)).Select(r => new FtaRate
            {
                TariffLineId = lineMap[r.TariffLineId], VersionId = to, SchemeCode = r.SchemeCode, PartnerCountryCode = r.PartnerCountryCode,
                RateValue = r.RateValue, RateUnit = r.RateUnit, ConditionsSummary = r.ConditionsSummary,
                EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, SourceId = r.SourceId, IsActive = r.IsActive,
            }));
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }
        foreach (var chunk in (await db.TaxRates.AsNoTracking().Where(r => r.VersionId == from).ToListAsync(ct)).Chunk(Chunk))
        {
            db.TaxRates.AddRange(chunk.Where(r => lineMap.ContainsKey(r.TariffLineId)).Select(r => new TaxRate
            {
                TariffLineId = lineMap[r.TariffLineId], VersionId = to, TaxType = r.TaxType, RateValue = r.RateValue, RateUnit = r.RateUnit,
                EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, SourceId = r.SourceId, IsActive = r.IsActive,
            }));
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        }

        // Legal notes
        db.SectionNotes.AddRange((await db.SectionNotes.AsNoTracking().Where(n => n.VersionId == from).ToListAsync(ct)).Select(n => new SectionNote
        {
            VersionId = to, SectionNumber = n.SectionNumber, NoteNumber = n.NoteNumber, ParagraphNumber = n.ParagraphNumber, TextId = n.TextId,
            TextEn = n.TextEn, EffectType = n.EffectType, TargetCodesJson = n.TargetCodesJson, ReferredCodesJson = n.ReferredCodesJson, SourceId = n.SourceId,
        }));
        db.ChapterNotes.AddRange((await db.ChapterNotes.AsNoTracking().Where(n => n.VersionId == from).ToListAsync(ct)).Select(n => new ChapterNote
        {
            VersionId = to, ChapterNumber = n.ChapterNumber, TargetHeading = n.TargetHeading, NoteNumber = n.NoteNumber, ParagraphNumber = n.ParagraphNumber,
            TextId = n.TextId, TextEn = n.TextEn, EffectType = n.EffectType, TargetCodesJson = n.TargetCodesJson, ReferredCodesJson = n.ReferredCodesJson, SourceId = n.SourceId,
        }));
        db.SubheadingNotes.AddRange((await db.SubheadingNotes.AsNoTracking().Where(n => n.VersionId == from).ToListAsync(ct)).Select(n => new SubheadingNote
        {
            VersionId = to, HeadingCode = n.HeadingCode, NoteNumber = n.NoteNumber, TextId = n.TextId, TextEn = n.TextEn, EffectType = n.EffectType,
            TargetCodesJson = n.TargetCodesJson, ReferredCodesJson = n.ReferredCodesJson, SourceId = n.SourceId,
        }));
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();

        // Explanatory Notes: paragraph links point at ids, so remap them once every copy has an id
        var en = await db.ExplanatoryNotes.AsNoTracking().Where(n => n.VersionId == from).OrderBy(n => n.Id).ToListAsync(ct);
        var copies2 = new List<(ExplanatoryNote Old, ExplanatoryNote New)>(en.Count);
        foreach (var chunk in en.Chunk(Chunk))
        {
            var pairs = chunk.Select(n => (Old: n, New: new ExplanatoryNote
            {
                VersionId = to, HeadingCode = n.HeadingCode, SubheadingCode = n.SubheadingCode, ParagraphNumber = n.ParagraphNumber,
                ParagraphType = n.ParagraphType, TextContent = n.TextContent, ReferredCodesJson = n.ReferredCodesJson, SourceId = n.SourceId,
            })).ToList();
            db.ExplanatoryNotes.AddRange(pairs.Select(p => p.New));
            await db.SaveChangesAsync(ct);
            copies2.AddRange(pairs);
        }
        var enMap = copies2.ToDictionary(p => p.Old.Id, p => p.New.Id);
        foreach (var (old, copy) in copies2)
        {
            copy.PreviousParagraphId = old.PreviousParagraphId is { } p && enMap.TryGetValue(p, out var np) ? np : null;
            copy.NextParagraphId = old.NextParagraphId is { } n && enMap.TryGetValue(n, out var nn) ? nn : null;
            copy.ParentSectionId = old.ParentSectionId is { } s && enMap.TryGetValue(s, out var ns) ? ns : null;
        }
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private async Task DeleteAsync(Guid versionId)
    {
        db.ChangeTracker.Clear();
        db.ExplanatoryNotes.RemoveRange(await db.ExplanatoryNotes.Where(n => n.VersionId == versionId).ToListAsync());
        db.SectionNotes.RemoveRange(await db.SectionNotes.Where(n => n.VersionId == versionId).ToListAsync());
        db.ChapterNotes.RemoveRange(await db.ChapterNotes.Where(n => n.VersionId == versionId).ToListAsync());
        db.SubheadingNotes.RemoveRange(await db.SubheadingNotes.Where(n => n.VersionId == versionId).ToListAsync());
        db.TaxRates.RemoveRange(await db.TaxRates.Where(r => r.VersionId == versionId).ToListAsync());
        db.FtaRates.RemoveRange(await db.FtaRates.Where(r => r.VersionId == versionId).ToListAsync());
        db.MfnRates.RemoveRange(await db.MfnRates.Where(r => r.VersionId == versionId).ToListAsync());
        db.BtkiTariffLines.RemoveRange(await db.BtkiTariffLines.Where(t => t.VersionId == versionId).ToListAsync());
        db.NomenclatureVersions.RemoveRange(await db.NomenclatureVersions.Where(v => v.Id == versionId).ToListAsync());
        await db.SaveChangesAsync();
    }
}
