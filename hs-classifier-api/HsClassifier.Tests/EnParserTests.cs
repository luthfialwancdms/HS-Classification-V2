using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HsClassifier.Infrastructure.Parsing;

namespace HsClassifier.Tests;

/// <summary>
/// Explanatory Notes parser rules, from what the real file looks like (docs/TEMUAN_EN.md): dotted bold heading
/// markers ("84.71- ..."), sentences wrapped over several paragraphs, OCR noise lines, English notes at the start
/// of each Chapter that are not EN paragraphs.
/// </summary>
public class EnParserTests
{
    private static int _i;
    private static RawLine L(string text, bool bold = false, bool list = false, bool table = false) =>
        new(++_i, text, bold, list, table);

    private static Application.Interfaces.EnParseResult Parse(params RawLine[] lines) =>
        EnTextParser.Parse(lines, "en.docx", "abc123");

    private static readonly string Long = "This heading covers machines used for the purpose described in the heading text.";

    [Fact]
    public void Dotted_bold_marker_opens_the_heading_and_its_title_is_not_a_paragraph()
    {
        var r = Parse(L("Chapter 84", true), L("Nuclear reactors, boilers, machinery", true),
            L("84.71- Automatic data processing machines and units thereof; magnetic or optical readers", true),
            L(Long));

        var p = Assert.Single(r.Paragraphs);
        Assert.Equal("8471", p.HeadingCode);
        Assert.Equal(1, p.SequenceInHeading);
        Assert.Equal(["8471"], r.Headings);
    }

    [Fact]
    public void Chapter_notes_and_chapter_general_text_are_not_stored_as_heading_paragraphs()
    {
        var r = Parse(L("Chapter 84", true), L("Notes.", true),
            L("1.- This Chapter does not cover: Millstones, grindstones or other articles of Chapter 68."),
            L("GENERAL", true), L("This Chapter covers machinery and mechanical appliances of every kind."),
            L("84.01- Nuclear reactors; fuel elements (cartridges), non-irradiated, for nuclear reactors", true),
            L(Long));

        Assert.Equal(["8401"], r.Paragraphs.Select(p => p.HeadingCode));
        Assert.Contains(r.Issues, i => i.Type == "CHAPTER_LEVEL_SKIPPED");
    }

    [Fact]
    public void A_sentence_wrapped_over_lines_becomes_one_paragraph()
    {
        var r = Parse(L("Chapter 1", true), L("01.06- Other live animals", true),
            L("Animals, including insects, which die during transport are classified in headings 02.01 to 02.05, 02.07,"),
            L("02.08 or 04.10 if they are edible animals fit for human consumption. In other cases they are classified in"),
            L("heading 05.11."));

        var p = Assert.Single(r.Paragraphs);
        Assert.Contains("02.07, 02.08 or 04.10", p.TextContent);
        Assert.EndsWith("heading 05.11.", p.TextContent);
    }

    [Fact]
    public void A_line_starting_in_lowercase_or_with_a_bracket_and_lowercase_continues_the_previous_one()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true),
            L("These machines have storage capability and also stored programs which can be changed from"),
            L("(job to job."),
            L("Automatic data processing machines process data in coded form."));

        Assert.Equal(2, r.Paragraphs.Count);
        Assert.EndsWith("changed from (job to job.", r.Paragraphs[0].TextContent);
    }

    [Fact]
    public void Noise_lines_are_dropped_and_counted()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true),
            L("("), L("12"), L("I"), L(" . "), L(Long));

        Assert.Single(r.Paragraphs);
        Assert.True(r.SkippedLines >= 4);
    }

    [Fact]
    public void A_garbled_heading_marker_is_recovered_from_its_subheading_listing()
    {
        // "84.7 1" is what OCR made of the marker; the listing "8471.30 - ..." still names the heading
        var r = Parse(L("Chapter 84", true), L("84.70- Weighing machinery", true), L(Long),
            L("84.7 1- Automatic data processing machines", true),
            L("8471.30 - Portable automatic data processing machines"),
            L("8471.41 - - Comprising in the same housing at least a central processing unit"),
            L("Data processing is the handling of information of all kinds, in pre-established logical sequences."));

        Assert.Equal(["8470", "8471"], r.Headings);
        Assert.Equal("8471", r.Paragraphs.Last().HeadingCode);
        Assert.DoesNotContain(r.Paragraphs, p => p.TextContent.StartsWith("8471.30"));   // the listing is not explanatory text
    }

    [Fact]
    public void A_cross_reference_to_another_chapter_is_not_a_heading()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true),
            L("85.17- Telephone sets, including smartphones", true),   // bold, but a different chapter
            L(Long));

        Assert.Equal(["8471"], r.Headings);
        Assert.Contains(r.Paragraphs, p => p.TextContent.StartsWith("85.17"));   // kept as text of 8471
    }

    [Fact]
    public void Heading_numbers_must_increase_within_a_chapter()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true), L(Long),
            L("84.02- A bold cross-reference that goes backwards", true), L(Long + " Second."));

        Assert.Equal(["8471"], r.Headings);
    }

    [Fact]
    public void Exclusion_lead_in_makes_the_following_list_items_exclusions_too()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true),
            L("The heading excludes machines, instruments or apparatus working in conjunction with such a machine."),
            L("This heading does not cover:"),
            L("Cash registers of heading 84.70.", list: true),
            L("Electronic calculators of heading 84.70.", list: true),
            L("This heading also covers separately presented constituent units."));

        Assert.Equal(["EXCLUSION", "EXCLUSION", "EXCLUSION", "EXCLUSION", "INCLUSION"], r.Paragraphs.Select(p => p.ParagraphType));
    }

    [Fact]
    public void The_word_excluded_in_the_middle_of_a_sentence_does_not_make_an_exclusion()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true),
            L("Machines which operate only on fixed programs are excluded even though the user may choose between programs."));

        Assert.Equal("SCOPE", Assert.Single(r.Paragraphs).ParagraphType);
    }

    [Fact]
    public void Referred_codes_come_from_dotted_and_plain_numbers()
    {
        var r = Parse(L("Chapter 1", true), L("01.06- Other live animals", true),
            L("Animals forming part of circuses (heading 95.08) and cultures of micro-organisms (heading 30.02)."));

        var codes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(Assert.Single(r.Paragraphs).ReferredCodesJson!)!;
        Assert.Equal(["9508", "3002"], codes);
    }

    [Fact]
    public void Source_id_points_at_the_first_word_paragraph_of_the_text()
    {
        var a = L("Chapter 1", true); var h = L("01.06- Other live animals", true); var p = L(Long);
        var r = Parse(a, h, p);

        Assert.Equal($"docx:abc123:en:0106:para_{p.Index}", Assert.Single(r.Paragraphs).SourceId);
    }

    [Fact]
    public void Table_cells_that_only_hold_codes_or_short_labels_are_skipped_but_prose_in_a_table_is_kept()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true),
            L("8471.70", table: true), L("- Storage units", table: true),
            L("The following machines are covered by this heading when presented as systems for processing data.", table: true));

        Assert.Single(r.Paragraphs);
    }

    [Fact]
    public void A_subheading_code_alone_in_a_table_cell_opens_its_heading_when_the_marker_line_is_missing()
    {
        // Chapter 1 in the real file: the title "- Live horses..." has lost its "01.01" marker; the table below still has "0101.21"
        var r = Parse(L("Chapter 1", true), L("- Live horses, asses, mules and bionics(+).", list: true),
            L("0101.21", table: true), L("- - Pure-bred breeding animals", table: true),
            L("This heading covers live horses, asses and mules, whether or not pure-bred."));

        Assert.Equal(["0101"], r.Headings);
        Assert.Equal("0101", Assert.Single(r.Paragraphs).HeadingCode);
    }
    [Fact]
    public void Titles_in_capitals_stay_separate_from_the_next_paragraph()
    {
        var r = Parse(L("Chapter 84", true), L("84.71- Automatic data processing machines", true),
            L("(I) AUTOMATIC DATA PROCESSING MACHINES AND UNITS THEREOF", true),
            L("Data processing is the handling of information of all kinds."));

        Assert.Equal(2, r.Paragraphs.Count);
        Assert.Equal("I", r.Paragraphs[0].ParagraphNumber);
    }

    [Fact]
    public void Text_before_the_first_chapter_is_front_matter_and_ignored()
    {
        var r = Parse(L("Harmonized Commodity Description and Coding System"), L("EXPLANATORY NOTES", true),
            L("Seventh edition (2022)"), L("Section I", true), L("Notes.", true), L("1.- Any reference in this Section to a particular genus."));

        Assert.Empty(r.Paragraphs);
    }

    // ─── Word extraction ──────────────────────────────────────────────────────

    private static MemoryStream Docx(Action<Body> build)
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body());
            build(main.Document.Body!);
            main.Document.Save();
        }
        ms.Position = 0;
        return ms;
    }

    private static Paragraph P(string text, bool bold = false, bool list = false)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        if (bold) run.PrependChild(new RunProperties(new Bold()));
        var p = new Paragraph(run);
        if (list) p.PrependChild(new ParagraphProperties(new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 1 })));
        return p;
    }

    [Fact]
    public async Task Word_file_deleted_and_hidden_text_is_left_out_and_bold_and_list_flags_are_read()
    {
        var deleted = new Paragraph(new Run(new Text("Chapter 84") { Space = SpaceProcessingModeValues.Preserve }), new DeletedRun(new Run(new DeletedText("REMOVED TEXT"))));
        var hidden = new Paragraph(new Run(new RunProperties(new Vanish()), new Text("HIDDEN TEXT")),
            new Run(new Text("Visible sentence that explains what the heading covers in some detail.")));
        using var docx = Docx(b => b.Append(deleted, P("84.71- Automatic data processing machines", bold: true), hidden,
            P("Cash registers of heading 84.70.", list: true)));

        var r = await new DocxParser().ParseENAsync(docx, "en.docx");

        Assert.Equal(["8471"], r.Headings);
        Assert.All(r.Paragraphs, p => Assert.DoesNotContain("REMOVED", p.TextContent));
        Assert.All(r.Paragraphs, p => Assert.DoesNotContain("HIDDEN", p.TextContent));
        Assert.Contains(r.Paragraphs, p => p.TextContent.StartsWith("Visible sentence"));
        Assert.Contains(r.Paragraphs, p => p.TextContent.StartsWith("Cash registers"));
    }
}
