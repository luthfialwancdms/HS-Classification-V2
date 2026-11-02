using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Tests;

/// <summary>
/// A result the database decided with confidence below High gets a second opinion from the AI: it is asked for tariff terms
/// and headings, and any heading the database confirms joins the candidates before the rules run again. The AI never sets
/// the code; it can only add headings for KUM HS 1 to 6 to weigh. A High result is trusted and never asks the AI.
/// </summary>
public class AiSecondOpinionTests
{
    private static readonly (string, string)[] Laptop =
    [
        ("generic_name", "Laptop"),
        ("primary_function", "mengolah data, dengan kamera, speaker dan mikrofon terintegrasi"),
    ];

    [Fact]
    public async Task A_decided_result_below_high_asks_the_ai_once_and_notes_it_in_the_reasoning()
    {
        var ai = new FakeSuggester(new TermSuggestion(["mesin pengolah data otomatis portabel"], ["8471"]));
        var m = await MiniBtki.CreateAsync(ai);

        var r = await m.ClassifyAsync(Laptop);

        Assert.Equal(1, ai.Calls);
        Assert.Equal("8471.30.20", r.FinalCode);
        var chain = await m.ChainAsync(r.CaseId);
        Assert.Contains(chain, l => l.StartsWith("AI_SECOND_OPINION|") && l.Contains("pos yang sama"));
    }

    [Fact]
    public async Task A_heading_the_ai_adds_and_the_database_confirms_is_weighed_by_the_rules()
    {
        var ai = new FakeSuggester(new TermSuggestion(["kamera digital"], ["8525"]));
        var m = await MiniBtki.CreateAsync(ai);

        var r = await m.ClassifyAsync(Laptop);

        var chain = await m.ChainAsync(r.CaseId);
        Assert.Contains(chain, l => l.StartsWith("AI_SECOND_OPINION|") && l.Contains("8525"));
        Assert.Contains(chain, l => l.StartsWith("KUM_HS_1|") && l.Contains("[8525]"));     // the added heading went through KUM HS 1
        Assert.NotNull(r.FinalCode);                                                         // and the rules still chose the code
    }

    [Fact]
    public async Task A_heading_the_database_does_not_know_is_ignored()
    {
        var ai = new FakeSuggester(new TermSuggestion(["x"], ["9999"]));
        var m = await MiniBtki.CreateAsync(ai);

        var r = await m.ClassifyAsync(Laptop);

        Assert.Equal("8471.30.20", r.FinalCode);
        Assert.DoesNotContain(await m.ChainAsync(r.CaseId), l => l.Contains("[9999]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_the_ai_has_nothing_or_is_unavailable_the_database_result_stands(bool unavailable)
    {
        var ai = new FakeSuggester(suggestion: null, unavailable: unavailable);
        var m = await MiniBtki.CreateAsync(ai);

        var r = await m.ClassifyAsync(Laptop);

        Assert.Equal(1, ai.Calls);
        Assert.Equal("8471.30.20", r.FinalCode);
        Assert.Equal("DETERMINED", r.Status);
    }

    [Fact]
    public async Task A_result_that_is_already_high_does_not_ask_the_ai()
    {
        var ai = new FakeSuggester(new TermSuggestion(["x"], ["8525"]));
        var m = await MiniBtki.CreateAsync(ai);
        var v = await m.Db.NomenclatureVersions.SingleAsync();
        // with notes and EN loaded for the heading, the confidence caps lift
        m.Db.ChapterNotes.Add(new ChapterNote { VersionId = v.Id, ChapterNumber = "84", NoteNumber = "1", EffectType = EffectType.Definition, TextId = "Catatan bab 84" });
        m.Db.SectionNotes.Add(new SectionNote { VersionId = v.Id, SectionNumber = "XVI", NoteNumber = "1", EffectType = EffectType.Definition, TextId = "Catatan bagian XVI" });
        m.Db.ExplanatoryNotes.Add(new ExplanatoryNote { VersionId = v.Id, HeadingCode = "8471", ParagraphType = ParagraphType.Scope, TextContent = "This heading covers data processing machines." });
        await m.Db.SaveChangesAsync();

        var r = await m.ClassifyAsync(("generic_name", "Laptop"), ("commercial_name", "Laptop Asus"),
            ("primary_function", "mesin pengolah data otomatis portabel, dengan kamera, speaker dan mikrofon terintegrasi"));

        Assert.Equal("8471.30.20", r.FinalCode);
        Assert.True(r.ConfidenceScore >= 80, $"score {r.ConfidenceScore} should be High for this check to mean anything");
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task The_first_ai_consultation_is_not_repeated_by_the_second_opinion()
    {
        // "Pompa air" is found by the head noun only, so the AI is asked once there; the decided result must not ask again
        var ai = new FakeSuggester(new TermSuggestion(["pompa vakum"], ["8414"]));
        var m = await MiniBtki.CreateAsync(ai);

        await m.ClassifyAsync(("generic_name", "Pompa air"));

        Assert.Equal(1, ai.Calls);
    }
}


