using HsClassifier.Application.Classification;
using HsClassifier.Application.Classification.Rules;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Tests;

/// <summary>
/// A legal note only removes a heading when one of its exclusion clauses names the goods as the excluded item and
/// sends them to a heading or chapter that does not cover the candidate. Words in parentheses are examples, not items.
/// </summary>
public class NoteExclusionTests
{
    private static CandidateHeading Cand(string code) => new() { Code = code, ChapterCode = code[..2], Description = "x" };

    private static ExclusionNote Note(string text, string origin = "Catatan Bab 84 no. 1") => new(origin, text, "pmk:ch84:1");

    private const string Ch84Note1 =
        "Bab ini tidak mencakup: (a) Batu gerinda (pos 68.04); (b) Mesin atau peralatan laboratorium (misalnya, pompa) dari kaca (pos 70.17); " +
        "(c) Pompa medis dan alat pernapasan (pos 90.18); (d) Traktor (Bab 87)";

    [Fact]
    public void A_word_in_parentheses_is_an_example_and_never_an_excluded_item()
    {
        Assert.Null(NoteExclusions.Find(["pompa"], Note(Ch84Note1), Cand("8413")));
    }

    [Fact]
    public void An_item_equal_to_the_goods_name_that_is_sent_elsewhere_excludes_the_candidate()
    {
        var hit = NoteExclusions.Find(["pompa medis"], Note(Ch84Note1), Cand("8413"));

        Assert.NotNull(hit);
        Assert.Contains("Pompa medis", hit);
        Assert.Contains("90.18", hit);
    }

    [Fact]
    public void One_item_in_a_list_is_enough()
    {
        Assert.NotNull(NoteExclusions.Find(["alat pernapasan"], Note(Ch84Note1), Cand("8413")));
    }

    [Fact]
    public void A_one_word_item_does_not_exclude_a_longer_name_that_merely_contains_it()
    {
        var note = Note("Bab ini tidak mencakup: (a) Pompa (pos 90.18)");

        Assert.Null(NoteExclusions.Find(["pompa air"], note, Cand("8413")));
        Assert.NotNull(NoteExclusions.Find(["pompa"], note, Cand("8413")));
    }

    [Fact]
    public void A_multi_word_item_inside_a_longer_name_excludes()
    {
        var note = Note("Bab ini tidak mencakup: (a) Pompa medis (pos 90.18)");

        Assert.NotNull(NoteExclusions.Find(["pompa medis portabel"], note, Cand("8413")));
    }

    [Fact]
    public void The_candidate_is_kept_when_the_clause_sends_the_goods_to_it()
    {
        var note = Note("Bab ini tidak mencakup: (a) Pompa udara (pos 84.14)");

        Assert.Null(NoteExclusions.Find(["pompa udara"], note, Cand("8414")));
        Assert.NotNull(NoteExclusions.Find(["pompa udara"], note, Cand("8413")));
    }

    [Fact]
    public void A_chapter_reference_covers_every_heading_of_that_chapter()
    {
        var note = Note("Bab ini tidak mencakup: (d) Traktor (Bab 87)");

        Assert.NotNull(NoteExclusions.Find(["traktor"], note, Cand("8429")));
        Assert.Null(NoteExclusions.Find(["traktor"], note, Cand("8701")));
    }

    [Fact]
    public void A_clause_that_sends_the_goods_nowhere_does_not_exclude()
    {
        Assert.Null(NoteExclusions.Find(["kertas"], Note("Bab ini tidak mencakup: (a) Kertas dan karton"), Cand("4802")));
    }

    [Fact]
    public void Several_names_are_all_tried_and_blank_or_tiny_names_are_ignored()
    {
        var note = Note("Bab ini tidak mencakup: (a) Pompa medis (pos 90.18)");

        Assert.NotNull(NoteExclusions.Find(["", "ab", "pompa medis"], note, Cand("8413")));
        Assert.Null(NoteExclusions.Find(["", "ab"], note, Cand("8413")));
    }

    [Fact]
    public void An_unrelated_candidate_chapter_is_still_excluded_only_by_its_own_item_match()
    {
        // The note belongs to chapter 84; the same clause text applied to any candidate decides by the item and the target only
        Assert.Null(NoteExclusions.Find(["batu apung"], Note(Ch84Note1), Cand("8413")));
    }

    // ─── RulesEngine: what the reasoning shows ──────────────────────────────

    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    private static CandidateHeading Pump(string code, string description, params ExclusionNote[] notes)
    {
        var c = new CandidateHeading
        {
            Code = code, ChapterCode = code[..2], Description = description, HasHeadingText = true, SourceId = $"btki:{code}",
            ChildSubheadings = [code + ".10.00"], ChildDescriptions = { [code + ".10.00"] = description },
        };
        c.ExclusionNotes.AddRange(notes);
        return c;
    }

    [Fact]
    public void Engine_eliminates_with_the_note_origin_the_excerpt_and_its_source_in_the_reasoning()
    {
        var medical = Note("Bab ini tidak mencakup: (c) Pompa medis (pos 90.18)", "Catatan Bab 84 no. 1");
        var pump = Pump("8413", "Pompa untuk cairan", medical);
        var other = Pump("9018", "Pompa medis dan instrumen kedokteran");

        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "pompa medis")), [pump, other]);

        var step = Assert.Single(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.CandidateCode == "8413");
        Assert.Equal(CandidateDecision.Eliminated, step.Decision);
        Assert.Contains("Catatan Bab 84 no. 1", step.Reason);
        Assert.Contains("Pompa medis", step.Reason);
        Assert.Contains("pmk:ch84:1", step.SourceIds);
        Assert.True(step.Reason.Length < 400);
    }

    [Fact]
    public void Engine_keeps_a_pump_heading_when_pompa_is_only_an_example_in_the_note()
    {
        var pump = Pump("8413", "Pompa untuk cairan", Note(Ch84Note1));

        var r = new RulesEngine().ApplyKumHs(Facts(("generic_name", "pompa")), [pump]);

        Assert.DoesNotContain(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.Decision == CandidateDecision.Eliminated);
        Assert.Contains(r.ReasoningChain, s => s.Rule == "KUM_HS_1" && s.Decision == CandidateDecision.Retained && s.CandidateCode == "8413");
    }
}
