using HsClassifier.Infrastructure.Parsing;

namespace HsClassifier.Tests;

/// <summary>
/// Catatan Bagian / Bab / Subpos from Lampiran II of PMK 26/PMK.010/2022 (docs/TEMUAN_EN.md): per Bab an Indonesian
/// block ("Bab 84", "Catatan.", "1.- ...") and then an English one ("Chapter 84", "Notes.", "1.- ..."); the first
/// Sections/Chapters have both languages glued in one paragraph.
/// </summary>
public class LegalNotesParserTests
{
    private static int _i;
    private static RawLine L(string text, bool bold = false, bool list = false) => new(++_i, text, bold, list, false);

    private static Application.Interfaces.LegalNotesParseResult Parse(params RawLine[] lines) =>
        LegalNotesTextParser.Parse(lines, "pmk.docx", "abc123");

    private static RawLine[] Chapter84() =>
    [
        L("Bab 84", true), L("Reaktor nuklir; ketel; mesin dan peralatan mekanis; bagian daripadanya", true), L("Catatan.", true),
        L("1.- Bab ini tidak meliputi:"),
        L("Batugiling, batu asah dan barang lainnya dari Bab 68;", list: true),
        L("Vacuum cleaner dari pos 85.08;", list: true),
        L("2.- Berdasarkan ketentuan dari Catatan 3 pada Bagian XVI, mesin atau peralatan harus diklasifikasikan dalam pos yang sesuai."),
        L("Chapter 84", true), L("Nuclear reactors, boilers, machinery and mechanical appliances; parts thereof", true), L("Notes.", true),
        L("1.- This Chapter does not cover:"),
        L("Millstones, grindstones or other articles of Chapter 68;", list: true),
        L("Vacuum cleaners of heading 85.08;", list: true),
        L("2.- Subject to the operation of Note 3 to Section XVI, a machine or appliance is to be classified under the appropriate heading."),
    ];

    [Fact]
    public void Indonesian_and_English_blocks_are_paired_by_note_number()
    {
        var r = Parse(Chapter84());

        Assert.Equal(["1", "2"], r.ChapterNotes.Select(n => n.NoteNumber));
        var n1 = r.ChapterNotes[0];
        Assert.Equal("84", n1.ScopeCode);
        Assert.StartsWith("1.- Bab ini tidak meliputi:", n1.TextId);
        Assert.Contains("Vacuum cleaner dari pos 85.08", n1.TextId);
        Assert.StartsWith("1.- This Chapter does not cover:", n1.TextEn);
        Assert.Contains("Vacuum cleaners of heading 85.08", n1.TextEn);
        Assert.Empty(r.SectionNotes);
    }

    [Fact]
    public void List_items_stay_inside_the_note_they_belong_to_one_per_line()
    {
        var n1 = Parse(Chapter84()).ChapterNotes[0];
        Assert.Equal(3, n1.TextId.Split('\n').Length);
        Assert.StartsWith("Batugiling", n1.TextId.Split('\n')[1]);
    }

    [Fact]
    public void Effect_type_is_read_from_the_opening_of_the_note_in_indonesian_or_english()
    {
        var r = Parse(Chapter84());
        Assert.Equal("EXCLUSION", r.ChapterNotes[0].EffectType);   // "Bab ini tidak meliputi"
        Assert.Equal("REFERRAL", r.ChapterNotes[1].EffectType);    // "harus diklasifikasikan dalam pos"
    }

    [Theory]
    [InlineData("1.- Bab ini tidak meliputi: a; b", "EXCLUSION")]
    [InlineData("1.- Bagian ini tidak mencakup limbah kota", "EXCLUSION")]
    [InlineData("1.- Pos ini tidak termasuk barang dari kaca", "EXCLUSION")]
    [InlineData("1.- Dalam Bab ini istilah \"pelet\" berarti produk yang telah diaglomerasi", "DEFINITION")]
    [InlineData("1.- Bab ini meliputi semua binatang hidup kecuali:", "INCLUSION")]
    [InlineData("1.- This Chapter does not cover: Millstones", "EXCLUSION")]
    [InlineData("1.- Dengan syarat bahwa barang dikemas eceran", "CONDITION")]
    public void Effect_type_examples(string note, string expected)
    {
        var r = Parse(L("Bab 5", true), L("Catatan.", true), L(note));
        Assert.Equal(expected, Assert.Single(r.ChapterNotes).EffectType);
    }

    [Fact]
    public void Section_notes_take_the_roman_number_even_when_the_english_title_is_glued_on()
    {
        var r = Parse(L("BAGIAN IISECTION II".Replace("IISECTION", " II SECTION"), true), L("PRODUK NABATI", true), L("Catatan.", true),
            L("1.- Bagian ini tidak meliputi barang dari Bab 4."),
            L("Notes.", true), L("1.- This Section does not cover goods of Chapter 4."));

        var n = Assert.Single(r.SectionNotes);
        Assert.Equal("II", n.ScopeCode);
        Assert.Contains("Bab 4", n.TextId);
        Assert.Contains("Chapter 4", n.TextEn);
    }

    [Fact]
    public void Glued_bilingual_layout_is_kept_in_one_text_and_reported()
    {
        var r = Parse(L("BAGIAN ISECTION I", true), L("BINATANG HIDUP; PRODUK HEWANILIVE ANIMALS; ANIMAL PRODUCTS", true), L("Catatan.Notes.", true),
            L("1.- Setiap referensi mengenai genus 1.- Any reference in this Section to a particular genus"),
            L("Bab 1Chapter 1", true), L("Binatang hidupLive animals", true), L("Catatan.Note.", true),
            L("1.- Bab ini meliputi semua binatang hidup kecuali: 1.- This Chapter covers all live animals except:"));

        Assert.Equal("I", Assert.Single(r.SectionNotes).ScopeCode);
        var c = Assert.Single(r.ChapterNotes);
        Assert.Equal("1", c.ScopeCode.TrimStart('0'));
        Assert.Contains("This Chapter covers", c.TextId);
        Assert.Equal(2, r.Issues.Count(i => i.Type == "MIXED_LANGUAGE"));
    }

    [Fact]
    public void Subheading_notes_belong_to_the_heading_they_name()
    {
        var r = Parse(L("Bab 85", true), L("Catatan.", true), L("1.- Bab ini tidak meliputi barang dari Bab 90."),
            L("Catatan Subpos.", true), L("1.- Untuk keperluan subpos 8517.13, yang dimaksud dengan telepon pintar adalah telepon seluler."));

        var n = Assert.Single(r.SubheadingNotes);
        Assert.Equal("8517", n.ScopeCode);
        Assert.Equal("1", n.NoteNumber);
        Assert.Single(r.ChapterNotes);
    }

    [Fact]
    public void A_note_without_a_number_at_the_start_becomes_note_1()
    {
        var r = Parse(L("Bab 6", true), L("Catatan.", true), L("Bab ini meliputi bunga dan tanaman hidup."));
        Assert.Equal("1", Assert.Single(r.ChapterNotes).NoteNumber);
    }

    [Fact]
    public void Only_english_text_is_stored_with_a_warning_because_the_engine_reads_the_indonesian_text()
    {
        var r = Parse(L("Chapter 9", true), L("Notes.", true), L("1.- This Chapter does not cover mixtures."));
        var n = Assert.Single(r.ChapterNotes);
        Assert.Equal("", n.TextId);
        Assert.Contains("mixtures", n.TextEn);
        Assert.Contains(r.Issues, i => i.Type == "ONLY_ENGLISH" && i.Severity == "WARNING");
    }

    [Fact]
    public void Only_the_annex_with_the_notes_is_read_when_the_annex_markers_exist()
    {
        var r = Parse(L("Bab 3", true), L("Catatan.", true), L("1.- Catatan dari Lampiran I yang tidak boleh ikut."),
            L("LAMPIRAN II"), L("Bab 4", true), L("Catatan.", true), L("1.- Bab ini tidak meliputi barang lain."),
            L("LAMPIRAN III"), L("Bab 5", true), L("Catatan.", true), L("1.- Dari Lampiran III."));

        Assert.Equal(["4"], r.ChapterNotes.Select(n => n.ScopeCode.TrimStart('0')));
    }

    [Fact]
    public void A_chapter_marker_may_carry_its_title_on_the_same_line()
    {
        var r = Parse(L("Bab22 Minuman, alkohol dan cuka", true), L("Catatan.", true), L("1.- Bab ini tidak meliputi produk dari Bab 21."),
            L("Chapter 22 Beverages, spirits and vinegar", true), L("Notes.", true), L("1.- This Chapter does not cover products of Chapter 21."));

        var n = Assert.Single(r.ChapterNotes);
        Assert.Equal("22", n.ScopeCode);
        Assert.Contains("Chapter 21", n.TextEn);
    }

    [Fact]
    public void A_note_that_merely_starts_with_bab_and_a_number_is_not_a_marker()
    {
        var r = Parse(L("Bab 72 Besi dan baja", true), L("Catatan.", true), L("1.- Dalam Bab ini istilah \"besi\" berarti logam."),
            L("Bab 72 tidak meliputi produk dari pos 73.01 atau 73.02."),
            L("Chapter 72 does not include products of heading 73.01 or 73.02."));

        var n = Assert.Single(r.ChapterNotes);
        Assert.Equal("72", n.ScopeCode);
        Assert.Contains("tidak meliputi produk dari pos 73.01", n.TextId);   // continued the note, did not open a new scope
    }

    [Fact]
    public void Indonesian_and_english_chunks_may_alternate_within_one_chapter()
    {
        // as in the real file: ID notes 1-2, then the English chunk, then ID notes continue
        var r = Parse(L("Bab 84", true), L("Catatan.", true),
            L("1.- Bab ini tidak meliputi:"), L("Batugiling dan barang lainnya dari Bab 68;", list: true),
            L("Chapter 84", true), L("Notes.", true),
            L("1.- This Chapter does not cover:"), L("Millstones and other articles of Chapter 68;", list: true),
            L("2.- Subject to the operation of Note 3, a machine is to be classified under the appropriate heading."),
            L("2.- Berdasarkan ketentuan Catatan 3, mesin harus diklasifikasikan dalam pos yang sesuai."));

        Assert.Equal(["1", "2"], r.ChapterNotes.Select(n => n.NoteNumber));
        Assert.All(r.ChapterNotes, n => { Assert.NotEmpty(n.TextId); Assert.NotNull(n.TextEn); });
        Assert.Contains("Batugiling", r.ChapterNotes[0].TextId);
        Assert.Contains("Millstones", r.ChapterNotes[0].TextEn);
        Assert.Empty(r.Issues.Where(i => i.Type == "ONLY_ENGLISH"));
    }
    [Fact]
    public void A_subheading_notes_marker_glued_to_its_english_twin_still_starts_subheading_notes()
    {
        var r = Parse(L("Bab 75Chapter 75", true), L("Nikel dan barang daripadanyaNickel and articles thereof", true), L("Catatan Subpos.Subheading Notes.", true),
            L("1.- Dalam bab ini, istilah berikut mempunyai arti: 1.- In this chapter the following expressions have"));

        Assert.Empty(r.ChapterNotes);
        var n = Assert.Single(r.SubheadingNotes);
        Assert.Equal("1", n.NoteNumber);
    }
    [Fact]
    public void Referred_codes_source_id_and_noise()
    {
        var a = L("Bab 84", true); var h = L("Catatan.", true); var noise = L("www.jdih.kemenkeu.go.id"); var note = L("1.- Bab ini tidak meliputi vacuum cleaner dari pos 85.08.");
        var r = Parse(a, h, noise, note);

        var n = Assert.Single(r.ChapterNotes);
        Assert.Equal("[\"8508\"]", n.ReferredCodesJson);
        Assert.Equal($"docx:abc123:notes:chapter:84:1:para_{note.Index}", n.SourceId);
        Assert.DoesNotContain("jdih", n.TextId);
    }
}
