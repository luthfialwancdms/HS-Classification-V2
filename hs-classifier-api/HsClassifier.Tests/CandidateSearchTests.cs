using HsClassifier.Application.Classification;
using HsClassifier.Domain.Classification;

namespace HsClassifier.Tests;

/// <summary>
/// Candidate search must start from the goods' name. The "Fungsi utama" dropdown label ("Lainnya", "Penggunaan rumah tangga")
/// is a category, not a description of the goods, and a trade name usually carries its head noun first ("Beras putih").
/// </summary>
public class CandidateSearchTests
{
    private static FactSet Facts(params (string, string)[] kv) =>
        new FactExtractionService().ExtractFromInput(kv.ToDictionary(x => x.Item1, x => (string?)x.Item2));

    [Fact]
    public async Task A_dropdown_category_label_is_never_searched_as_text()
    {
        var m = await MiniBtki.CreateAsync();

        // "lainnya" appears in many tariff lines; as a search term it used to pull in unrelated headings
        var found = await m.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "zzz qqq"), ("primary_function", "Lainnya")));

        Assert.Empty(found);
    }

    [Fact]
    public async Task Free_text_function_is_still_searched_when_the_name_finds_nothing()
    {
        var m = await MiniBtki.CreateAsync();

        var found = await m.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "zzz qqq"), ("primary_function", "pengeras suara")));

        Assert.Contains(found, c => c.Code == "8518");
    }

    [Fact]
    public async Task A_two_word_name_is_searched_by_its_head_noun_against_headings()
    {
        var m = await MiniBtki.CreateAsync();

        var found = await m.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "Pompa air")));

        Assert.Contains(found, c => c.Code == "8413" && c.GenerationSource == "HEADNOUN");
        Assert.Contains(found, c => c.Code == "8414");
    }

    [Fact]
    public async Task The_head_noun_matches_whole_words_only()
    {
        var m = await MiniBtki.CreateAsync();

        // "kapa" is only part of the word "kapal" in 8901 and 8905
        var found = await m.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "Kapa besar")));

        Assert.DoesNotContain(found, c => c.Code == "8901");
    }

    [Fact]
    public async Task A_name_that_matches_as_a_whole_phrase_does_not_fall_back_to_the_head_noun()
    {
        var m = await MiniBtki.CreateAsync();

        var found = await m.Candidates.GenerateCandidatesAsync(Facts(("generic_name", "Pompa vakum")));

        Assert.DoesNotContain(found, c => c.GenerationSource == "HEADNOUN");
    }
}


