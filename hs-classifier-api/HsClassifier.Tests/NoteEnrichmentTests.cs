using HsClassifier.Application.Classification;
using HsClassifier.Domain.Classification;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>Candidates carry the section and chapter notes and the EN of their heading, and the engine reads them with care.</summary>
public class NoteEnrichmentTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static async Task<(MiniBtki Mini, Guid VersionId)> WithNotes()
    {
        var mini = await MiniBtki.CreateAsync();
        var v = await mini.Db.NomenclatureVersions.SingleAsync();
        mini.Db.SectionNotes.Add(new SectionNote { VersionId = v.Id, SectionNumber = "XVI", NoteNumber = "1", EffectType = EffectType.Exclusion,
            TextId = "Bagian ini tidak mencakup: (a) Sabuk pengangkut dari plastik (Bab 39)", SourceId = "pmk:s16:1" });
        mini.Db.ChapterNotes.Add(new ChapterNote { VersionId = v.Id, ChapterNumber = "84", NoteNumber = "1", EffectType = EffectType.Exclusion,
            TextId = "Bab ini tidak mencakup: (a) Pompa medis (pos 90.18); (b) Mesin (misalnya, pompa) dari kaca (pos 70.17)", SourceId = "pmk:ch84:1" });
        mini.Db.ChapterNotes.Add(new ChapterNote { VersionId = v.Id, ChapterNumber = "84", NoteNumber = "2", EffectType = EffectType.Definition,
            TextId = "Dalam bab ini, \"mesin\" berarti ...", SourceId = "pmk:ch84:2" });
        mini.Db.ChapterNotes.Add(new ChapterNote { VersionId = v.Id, ChapterNumber = "87", NoteNumber = "1", EffectType = EffectType.Exclusion,
            TextId = "Bab ini tidak mencakup: (a) Dumper (Bab 84)" });
        mini.Db.ExplanatoryNotes.Add(new ExplanatoryNote { VersionId = v.Id, HeadingCode = "8413", ParagraphType = ParagraphType.Exclusion,
            ParagraphNumber = "1", TextContent = "This heading does not cover: (a) Pumps specially designed for medical use (heading 90.18).", SourceId = "en:8413:1" });
        await mini.Db.SaveChangesAsync();
        return (mini, v.Id);
    }

    [Fact]
    public async Task Candidates_carry_chapter_notes_section_notes_and_en_from_their_own_scope_only()
    {
        var (mini, _) = await WithNotes();

        var pump = (await mini.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "Pompa")))).Single(c => c.Code == "8413");

        Assert.Equal("XVI", pump.SectionCode);
        Assert.Equal(2, pump.ChapterNotesText.Count);                    // the two chapter 84 notes, not the chapter 87 one
        Assert.Single(pump.SectionNotesText);
        Assert.Single(pump.EnParagraphs);
        Assert.Equal(new[] { "Catatan Bab 84 no. 1", "Catatan Bagian XVI no. 1", "EN pos 8413 no. 1" }.Order(),
            pump.ExclusionNotes.Select(n => n.Origin).Order());
    }

    [Fact]
    public async Task Pump_named_only_as_an_example_in_the_chapter_note_is_not_eliminated()
    {
        var (mini, _) = await WithNotes();

        var r = await mini.ClassifyAsync(("generic_name", "Pompa"));

        var chain = await mini.ChainAsync(r.CaseId);
        Assert.DoesNotContain(chain, s => s.StartsWith("KUM_HS_1|Eliminated") && s.Contains("Catatan Bab 84"));
    }

    [Fact]
    public async Task Medical_pump_is_eliminated_from_the_pump_heading_with_the_note_and_its_source_in_the_reasoning()
    {
        var (mini, _) = await WithNotes();
        // The pump heading comes from the "Pompa" search; the engine then judges the medical-pump facts against it
        var candidates = await mini.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "Pompa")));

        var result = new HsClassifier.Application.Classification.Rules.RulesEngine()
            .ApplyKumHs(Facts(("generic_name", "Pompa medis")), candidates.Where(c => c.Code == "8413").ToList());

        var step = result.ReasoningChain.First(s => s.Rule == "KUM_HS_1" && s.CandidateCode == "8413");
        Assert.Equal(CandidateDecision.Eliminated, step.Decision);
        Assert.Contains("Pompa medis", step.Reason);
        Assert.Contains("pmk:ch84:1", step.SourceIds);
    }

    [Fact]
    public async Task Notes_of_another_version_are_not_loaded()
    {
        var (mini, _) = await WithNotes();
        var other = new NomenclatureVersion { Name = "Draft", EffectiveFrom = new DateTime(2023, 1, 1), State = ImportState.Indexed };
        mini.Db.NomenclatureVersions.Add(other);
        mini.Db.ChapterNotes.Add(new ChapterNote { VersionId = other.Id, ChapterNumber = "84", NoteNumber = "9", EffectType = EffectType.Exclusion, TextId = "draft only" });
        await mini.Db.SaveChangesAsync();

        var pump = (await mini.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "Pompa")))).Single(c => c.Code == "8413");

        Assert.DoesNotContain(pump.ExclusionNotes, n => n.Text == "draft only");
    }

    [Fact]
    public async Task Without_notes_nothing_is_loaded_and_nothing_counts_as_tested()
    {
        var mini = await MiniBtki.CreateAsync();

        var pump = (await mini.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "Pompa")))).Single(c => c.Code == "8413");

        Assert.Empty(pump.ExclusionNotes);
        Assert.Empty(pump.ChapterNotesText);
        Assert.Empty(pump.SectionNotesText);
        Assert.Empty(pump.EnParagraphs);
    }

    [Fact]
    public void Chapters_map_to_their_section()
    {
        Assert.Equal("XVI", HsSections.ForChapter("84"));
        Assert.Equal("XVII", HsSections.ForChapter("87"));
        Assert.Equal("I", HsSections.ForChapter("01"));
        Assert.Equal("XXI", HsSections.ForChapter("99"));
        Assert.Null(HsSections.ForChapter("abc"));
        Assert.Null(HsSections.ForChapter(null));
    }
}
