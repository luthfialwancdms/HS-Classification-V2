using HsClassifier.Application.Classification.Rules;
using HsClassifier.Infrastructure.Services;

namespace HsClassifier.Tests;

/// <summary>
/// The six regression cases of the V2 master prompt (TESTING WAJIB) run through the real pipeline on a small BTKI
/// fixture (<see cref="MiniBtki"/>). The expected outcomes for dumper and crane are not confirmed by a customs
/// expert, so those tests only pin what is certain: the right heading is evaluated and nothing is decided on the
/// goods' trade name. Tests marked Skip are gaps this fixture exposed; see AUDIT_FITUR.md.
/// </summary>
public class RegressionCasesTests
{
    // 1. "Pompa industri" must not become a centrifugal pump with high confidence.
    [Fact]
    public async Task Pompa_industri_alone_is_not_decided()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "pompa industri"));

        Assert.Null(r.FinalCode);
        // Since the head noun "pompa" is searched against headings, the pump headings are found and the user is asked,
        // instead of going straight to an expert as before
        Assert.Equal("NEED_CLARIFICATION", r.Status);
        Assert.NotEmpty(r.Questions!);
    }

    [Fact]
    public async Task Pompa_alone_asks_for_the_subheading_and_decides_nothing()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "pompa"));

        Assert.Null(r.FinalCode);
        Assert.Equal("NEED_CLARIFICATION", r.Status);
        var q = Assert.Single(r.Questions!);
        Assert.Equal("subheading_choice", q.FactType);
        Assert.Contains(RulesEngine.NoneChoice, q.Choices!.Select(c => c.Value));   // "none of these" always offered
    }

    [Fact(Skip = "Known gap: with only \"pompa\", 8413 (liquids) and 8414 (air/vacuum) tie and KUM HS 3(a) picks 8413 on a text score, " +
                 "so the user is never asked whether the pump moves liquid or air. The master prompt asks for the pump type.")]
    public async Task Pump_for_liquid_or_air_is_asked_before_a_heading_is_chosen()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "pompa"));
        Assert.DoesNotContain(r.Questions!, q => q.FactType == "subheading_choice");
    }

    // 4. Laptop with camera, speaker and microphone: tested from the database, not from a stored answer.
    [Fact]
    public async Task Laptop_with_camera_speaker_and_microphone_is_classified_as_8471_30_20()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "Laptop"),
            ("primary_function", "mengolah data, dengan kamera, speaker dan mikrofon terintegrasi"));

        Assert.Equal("DETERMINED", r.Status);
        Assert.Equal("8471.30.20", r.FinalCode);
        Assert.True((await m.Validator.ValidateAsync(r.FinalCode)).IsValid);
        Assert.DoesNotContain(await m.ChainAsync(r.CaseId), l => l.StartsWith("AI_SUGGESTION"));
        // the camera and microphone headings did not take over just because the facts mention them
        Assert.DoesNotContain("8525", r.CandidateCodes ?? []);
        Assert.DoesNotContain("8518", r.CandidateCodes ?? []);
    }

    [Fact]
    public async Task Laptop_heading_is_found_by_database_text_not_only_by_the_synonym_list()
    {
        var m = await MiniBtki.CreateAsync();
        var facts = new HsClassifier.Application.Classification.FactExtractionService()
            .ExtractFromInput(new() { ["generic_name"] = "Laptop" });
        var found = (await m.Candidates.GenerateCandidatesAsync(facts)).Single(c => c.Code == "8471");

        Assert.Equal("LEXICAL", found.GenerationSource);   // SYNONYM would mean only the hard-coded map found it
    }

    [Fact]
    public async Task Laptop_described_without_the_word_laptop_still_reaches_8471_and_asks_between_its_subheadings()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "mesin pengolah data otomatis portabel"));

        Assert.Null(r.FinalCode);
        Assert.Equal(["8471"], r.CandidateCodes);
        var q = Assert.Single(r.Questions!);
        Assert.Contains("8471.3", q.Choices!.Select(c => c.Value));
    }

    // 5. A dumper whose trade name says "tractor": the objective facts decide.
    [Fact]
    public async Task Dumper_named_tractor_is_decided_by_what_it_does_not_by_its_name()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "Dumper"), ("commercial_name", "Tractor"),
            ("primary_function", "pengangkutan barang"));

        Assert.Equal("8704.10.00", r.FinalCode);
        var chain = await m.ChainAsync(r.CaseId);
        Assert.Contains(chain, l => l.StartsWith("KUM_HS_1|Retained|[8701]"));   // the trade name's heading was weighed too
        Assert.Contains(chain, l => l.StartsWith("KUM_HS_1|Retained|[8704]"));
    }

    [Fact(Skip = "Known gap: described in plain words (\"mengangkut material dengan bak pembuang\") the dumper shares no word with the " +
                 "official text of 8704 (\"pengangkutan barang\"; \"Dumper\" appears only in a child line), so KUM HS 1 eliminates 8704 " +
                 "and keeps 8701 because of the trade name \"Tractor\". The user is then asked to pick a tractor subheading.")]
    public async Task Dumper_named_tractor_described_in_plain_words_is_not_sent_to_the_tractor_heading()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "Dumper"), ("commercial_name", "Tractor"),
            ("primary_function", "mengangkut material dengan bak pembuang"));

        Assert.DoesNotContain(r.CandidateCodes ?? [], c => c == "8701");
        Assert.DoesNotContain(r.Questions ?? [], q => q.QuestionText.Contains("8701"));
    }

    // 6. Crane fixed on a barge: every relevant heading is tested, not just the first.
    [Fact]
    public async Task Crane_on_a_barge_weighs_both_the_crane_heading_and_the_floating_crane_heading()
    {
        var m = await MiniBtki.CreateAsync();
        var r = await m.ClassifyAsync(("generic_name", "crane"), ("primary_function", "mengangkat muatan"),
            ("mounting", "terpasang tetap di atas tongkang (barge)"));

        var chain = await m.ChainAsync(r.CaseId);
        Assert.Contains(chain, l => l.StartsWith("KUM_HS_1|Retained|[8426]"));
        Assert.Contains(chain, l => l.StartsWith("KUM_HS_1|Retained|[8905]"));
        Assert.Null(r.FinalCode);                       // not decided without the user confirming a subheading
        Assert.Equal("NEED_CLARIFICATION", r.Status);
    }
}

/// <summary>Case 254: a pen flagged as a retail set whose set tests answer "no" must still be classified, not parked for an expert.</summary>
public class NotARetailSetTests
{
    [Fact]
    public async Task Pen_that_fails_the_set_tests_is_classified_and_the_ai_term_helper_is_tried()
    {
        var ai = new FakeSuggester(new TermSuggestion(["pena"], ["8471"]));
        var m = await MiniBtki.CreateAsync(ai);
        var r = await m.ClassifyAsync(("commercial_name", "Pulpen Pilot"), ("generic_name", "Pulpen"),
            ("set_kit", "retail_set"), ("set_single_purpose", "no"), ("set_retail_ready", "yes"), ("set_essential_item", "pulpen"));

        var chain = await m.ChainAsync(r.CaseId);
        Assert.Contains(chain, l => l.StartsWith("KUM_HS_3B|Retained") && l.Contains("bukan set"));
        Assert.Equal(1, ai.Calls);                                   // the database found nothing, so the AI was asked
        Assert.Contains(chain, l => l.StartsWith("AI_SUGGESTION"));
        Assert.DoesNotContain("KUM HS 3(b): bukan set untuk penjualan eceran", r.Message ?? "");
    }
}

