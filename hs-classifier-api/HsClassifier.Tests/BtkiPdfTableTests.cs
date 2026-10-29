using HsClassifier.Application.Interfaces;
using HsClassifier.Application.Services;

namespace HsClassifier.Tests;

/// <summary>Sketches the BTKI PDF layout: code at x=74, Indonesian text at x=114, English text at x=421.</summary>
internal static class PdfSketch
{
    private const double Char = 3.6;

    public static IEnumerable<PdfWordBox> Row(double y, string? code, string id, string en, double idLeft = 114)
    {
        if (code is not null) yield return new(code, 74, 74 + code.Length * Char, y);
        foreach (var w in Words(id, idLeft, y)) yield return w;
        foreach (var w in Words(en, 421, y)) yield return w;
    }

    private static IEnumerable<PdfWordBox> Words(string text, double x, double y)
    {
        foreach (var t in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return new(t, x, x + t.Length * Char, y);
            x += t.Length * Char + 3;
        }
    }

    public static IEnumerable<PdfWordBox> Header()
    {
        yield return new("Pos", 81, 92, 534); yield return new("Tarif", 93, 108, 534);
        yield return new("Uraian", 248, 265, 530); yield return new("Barang", 266, 290, 530);
        yield return new("Description", 543, 572, 530); yield return new("of", 573, 579, 530); yield return new("Goods", 580, 602, 530);
        yield return new("HS", 81, 89, 526); yield return new("Code", 90, 108, 526);
    }

    public static PdfPageWords Page(int no, bool header, params IEnumerable<PdfWordBox>[] rows) =>
        new(no, 792, 612, (header ? Header() : []).Concat(rows.SelectMany(r => r)).ToList());
}

public class BtkiTableAssemblerTests
{
    private static BtkiAssembly Run(List<ParseIssue> issues, params PdfPageWords[] pages) =>
        BtkiTableAssembler.Assemble(pages, issues);

    [Fact]
    public void Heading_wraps_over_two_lines_in_both_languages()
    {
        var issues = new List<ParseIssue>();
        var a = Run(issues, PdfSketch.Page(1, true,
            PdfSketch.Row(520, "74.01", "Mate tembaga; tembaga semen", "Copper mattes;"),
            PdfSketch.Row(512, null, "(tembaga endapan).", "cement copper."),
            PdfSketch.Row(504, "7401.00.10", "- Mate tembaga", "- Copper mattes")));

        Assert.Equal(2, a.Rows.Count);
        Assert.Equal((BtkiRowKind.Heading, "7401"), (a.Rows[0].Kind, a.Rows[0].Code));
        Assert.Equal("Mate tembaga; tembaga semen (tembaga endapan).", a.Rows[0].TextId);
        Assert.Equal("Copper mattes; cement copper.", a.Rows[0].TextEn);
        Assert.Equal((BtkiRowKind.Line, "7401.00.10", 1), (a.Rows[1].Kind, a.Rows[1].Code, a.Rows[1].Depth));
        Assert.Equal(3, a.SkippedLines);   // the three header lines
        Assert.Empty(issues);
    }

    [Fact]
    public void Code_formats_map_to_kinds_and_normalised_codes()
    {
        var a = Run([], PdfSketch.Page(1, true,
            PdfSketch.Row(520, "74.07", "Batang tembaga.", "Copper bars."),
            PdfSketch.Row(512, "7407.10", "- Dari tembaga dimurnikan :", "- Of refined copper:"),
            PdfSketch.Row(504, "7407.10.41", "- - - Dengan penampang bujur sangkar", "- - - Of square cross-section"),
            PdfSketch.Row(496, "7409.1", "- Dari tembaga dimurnikan :", "- Of refined copper:")));

        Assert.Equal(
            [(BtkiRowKind.Heading, "7407"), (BtkiRowKind.Subheading, "7407.10"), (BtkiRowKind.Line, "7407.10.41"), (BtkiRowKind.Group, "7409.1")],
            a.Rows.Select(r => (r.Kind, r.Code)));
        Assert.Equal([0, 1, 3, 1], a.Rows.Select(r => r.Depth));
    }

    [Fact]
    public void Dashed_line_without_code_is_a_group_row()
    {
        var a = Run([], PdfSketch.Page(1, true,
            PdfSketch.Row(520, "84.29", "Buldoser, ekskavator.", "Bulldozers."),
            PdfSketch.Row(512, null, "- Sekop mekanik, ekskavator dan shovel loader :", "- Mechanical shovels:"),
            PdfSketch.Row(504, "8429.51.00", "- - Shovel loader bagian depan", "- - Front-end shovel loaders")));

        var group = a.Rows[1];
        Assert.Equal((BtkiRowKind.Group, (string?)null, 1), (group.Kind, group.Code, group.Depth));
        Assert.Equal("- Sekop mekanik, ekskavator dan shovel loader :", group.TextId);
        Assert.Equal("8429.51.00", a.Rows[2].Code);
    }

    [Fact]
    public void Repeated_header_is_dropped_and_a_row_continues_across_pages()
    {
        var a = Run([],
            PdfSketch.Page(1, true, PdfSketch.Row(100, "74.10", "Foil tembaga (dicetak atau dengan alas kertas)", "Copper foil")),
            PdfSketch.Page(2, true,
                PdfSketch.Row(520, null, "dengan ketebalan tidak melebihi 0,15 mm.", "of a thickness not exceeding 0.15 mm."),
                PdfSketch.Row(512, "7410.11.10", "- - Palutan tembaga", "- - Copper clad")));

        Assert.Equal(2, a.Rows.Count);
        Assert.Equal("Foil tembaga (dicetak atau dengan alas kertas) dengan ketebalan tidak melebihi 0,15 mm.", a.Rows[0].TextId);
        Assert.Equal("Copper foil of a thickness not exceeding 0.15 mm.", a.Rows[0].TextEn);
        Assert.Equal(6, a.SkippedLines);   // two pages × three header lines
    }

    [Fact]
    public void Indonesian_word_ending_at_the_corridor_edge_is_not_moved_into_english()
    {
        var page = new PdfPageWords(1, 792, 612,
        [
            .. PdfSketch.Header(),
            .. PdfSketch.Row(520, "7408.11.20", "- - - Dengan", "- - - Of which"),
            new PdfWordBox("melebihi", 380, 417.4, 520),
            .. PdfSketch.Row(512, "7408.11.90", "- - - Lain-lain", "- - - Other"),
        ]);
        var a = BtkiTableAssembler.Assemble([page], []);
        Assert.Contains("melebihi", a.Rows[0].TextId);
        Assert.DoesNotContain("melebihi", a.Rows[0].TextEn);
    }

    [Fact]
    public void Code_alone_on_its_line_takes_the_dashed_text_printed_just_below_it()
    {
        // real layout of 4403.23: the code sits 4 units above the first text line
        var a = Run([], PdfSketch.Page(1, true,
            PdfSketch.Row(293, "4403.22.90", "- - - Lain-lain", "- - - Other"),
            PdfSketch.Row(286, "4403.23", "", ""),
            PdfSketch.Row(282, null, "- - Dari fir (Abies spp .) dan spruce (Picea spp .) :", "- - Of fir (Abies spp.) and spruce (Picea spp.):"),
            PdfSketch.Row(271, "4403.23.10", "- - - Baulk, sawlog dan veneer log", "- - - Baulks")));

        Assert.Equal(["4403.22.90", "4403.23", "4403.23.10"], a.Rows.Select(r => r.Code));
        Assert.Equal(BtkiRowKind.Subheading, a.Rows[1].Kind);
        Assert.Equal("- - Dari fir (Abies spp .) dan spruce (Picea spp .) :", a.Rows[1].TextId);
        Assert.Equal(2, a.Rows[1].Depth);
    }

    [Fact]
    public void Superscript_digit_is_glued_to_its_word_and_does_not_split_the_row()
    {
        // real layout of 5603.11.00: the "2" of g/m2 sits 3 above the text baseline, on the same height as the code
        var page = new PdfPageWords(1, 792, 612,
        [
            .. PdfSketch.Header(),
            new PdfWordBox("5603.11.00", 74, 110, 391.3),
            new PdfWordBox("-", 114, 116, 390.1), new PdfWordBox("-", 117, 119, 390.1),
            new PdfWordBox("Beratnya", 120.5, 143.5, 390.1), new PdfWordBox("25", 183.7, 190.1, 390.1),
            new PdfWordBox("g/m", 191.7, 201.3, 390.1), new PdfWordBox("2", 201.2, 203.4, 393.0, 3.9),
            new PdfWordBox("tetapi", 205, 220, 390.1),
            new PdfWordBox("-", 421.4, 423.4, 390.1), new PdfWordBox("Weighing", 428.4, 452.7, 390.1),
            new PdfWordBox("g/m", 499.6, 509.2, 390.1), new PdfWordBox("2", 509.2, 511.3, 393.0, 3.9),
            new PdfWordBox("5603.12.00", 74, 110, 382.8),
            new PdfWordBox("-", 114, 116, 381.6), new PdfWordBox("-", 117, 119, 381.6), new PdfWordBox("Lain-lain", 120.5, 150, 381.6),
            new PdfWordBox("-", 421.4, 423.4, 381.6), new PdfWordBox("Other", 428.4, 446, 381.6),
        ]);
        var a = BtkiTableAssembler.Assemble([page], []);

        Assert.Equal(["5603.11.00", "5603.12.00"], a.Rows.Select(r => r.Code));
        Assert.Equal("- - Beratnya 25 g/m\u00B2 tetapi", a.Rows[0].TextId);
        Assert.Equal("- Weighing g/m\u00B2", a.Rows[0].TextEn);
    }

    [Fact]
    public void Chemical_subscripts_form_one_word_and_hyphen_wrapped_words_are_rejoined()
    {
        var page = new PdfPageWords(1, 792, 612,
        [
            .. PdfSketch.Header(),
            new PdfWordBox("6903.20.00", 74, 110, 300),
            new PdfWordBox("-", 114, 116, 300), new PdfWordBox("Mengandung", 118, 150, 300),
            new PdfWordBox("(Al", 152, 160, 300), new PdfWordBox("2", 160.1, 162, 298.4, 3.9), new PdfWordBox("O", 162.2, 166, 300),
            new PdfWordBox("3", 166.1, 168, 298.4, 3.9), new PdfWordBox("),", 168.2, 172, 300),
            new PdfWordBox("digunakan", 175, 200, 300), new PdfWordBox("semata-", 203, 225, 300),
            new PdfWordBox("mata", 114, 130, 292),
        ]);
        var a = BtkiTableAssembler.Assemble([page], []);

        Assert.Equal("- Mengandung (Al\u2082O\u2083), digunakan semata-mata", a.Rows.Single().TextId);
    }

    [Fact]
    public void Page_number_is_skipped_and_leading_text_without_code_is_reported()
    {
        var issues = new List<ParseIssue>();
        var a = Run(issues, PdfSketch.Page(1, true,
            PdfSketch.Row(520, null, "lanjutan tanpa induk", "orphan"),
            PdfSketch.Row(512, "74.04", "Sisa dan skrap tembaga.", "Copper waste and scrap."),
            [new PdfWordBox("12", 390, 396, 20)]));

        Assert.Single(a.Rows);
        Assert.Equal("Sisa dan skrap tembaga.", a.Rows[0].TextId);
        Assert.Contains(issues, i => i.Type == "ORPHAN_LINE");
    }

    [Fact]
    public void Unrecognised_code_is_reported_not_silently_dropped()
    {
        var issues = new List<ParseIssue>();
        var a = Run(issues, PdfSketch.Page(1, true,
            PdfSketch.Row(520, "74.04", "Sisa dan skrap tembaga.", "Scrap."),
            PdfSketch.Row(512, "7404.0O.00", "- Lain-lain", "- Other")));

        Assert.Contains(issues, i => i.Type == "UNPARSEABLE_CODE");
        Assert.Equal(BtkiRowKind.Group, a.Rows[1].Kind);   // text kept, as an uncoded group
    }
}

public class BtkiHierarchyBuilderTests
{
    private static BtkiTableRow Row(BtkiRowKind kind, string? code, string id, int page = 1, int seq = 1, string en = "") =>
        new() { Kind = kind, Code = code, TextId = id, TextEn = en, Page = page, Seq = seq };

    private static (List<ParsedTariffNode> Nodes, List<ParseIssue> Issues, List<string> Missing) Build(params BtkiTableRow[] rows)
    {
        var issues = new List<ParseIssue>(); var missing = new List<string>();
        return (BtkiHierarchyBuilder.Build(rows, "t.pdf", issues, missing), issues, missing);
    }

    [Fact]
    public void Lines_hang_under_their_subheading_and_subheadings_under_the_heading()
    {
        var (nodes, issues, _) = Build(
            Row(BtkiRowKind.Heading, "7407", "Batang tembaga."),
            Row(BtkiRowKind.Subheading, "7407.10", "- Dari tembaga dimurnikan :", seq: 2),
            Row(BtkiRowKind.Line, "7407.10.30", "- - Profil", seq: 3),
            Row(BtkiRowKind.Line, "7407.29.00", "- - Lain-lain", seq: 4));

        string? ParentOf(string c) => nodes.Single(n => n.Code == c).ParentCode;
        Assert.Null(ParentOf("7407"));
        Assert.Equal("7407", ParentOf("7407.10"));
        Assert.Equal("7407.10", ParentOf("7407.10.30"));
        Assert.Equal("7407", ParentOf("7407.29.00"));          // no subheading row: straight under the heading
        Assert.Equal("pdf:t.pdf:p1:row_3", nodes.Single(n => n.Code == "7407.10.30").SourceId);
        Assert.Equal("XV", nodes.Single(n => n.Code == "7407").SectionCode);
        Assert.DoesNotContain(issues, i => i.Severity != "INFO");
    }

    [Fact]
    public void Uncoded_one_dash_group_gets_the_code_of_the_next_coded_row()
    {
        var (nodes, _, _) = Build(
            Row(BtkiRowKind.Heading, "8429", "Buldoser, ekskavator."),
            Row(BtkiRowKind.Group, null, "- Sekop mekanik, ekskavator dan shovel loader :", seq: 2),
            Row(BtkiRowKind.Line, "8429.51.00", "- - Shovel loader bagian depan", seq: 3));

        var group = nodes.Single(n => n.Code == "8429.5");
        Assert.Equal("8429", group.ParentCode);
        Assert.Equal("- Sekop mekanik, ekskavator dan shovel loader :", group.DescriptionId);
        Assert.Equal("8429.5", nodes.Single(n => n.Code == "8429.51.00").ParentCode);
    }

    [Fact]
    public void Heading_printed_only_as_a_single_tariff_line_is_created_from_that_line()
    {
        var (nodes, issues, _) = Build(
            Row(BtkiRowKind.Heading, "7403", "Tembaga dimurnikan."),
            Row(BtkiRowKind.Line, "7404.00.00", "Sisa dan skrap tembaga.", seq: 2, en: "Copper waste and scrap."));

        var heading = nodes.Single(n => n.Code == "7404");
        Assert.Equal("Sisa dan skrap tembaga.", heading.DescriptionId);
        Assert.Equal("Copper waste and scrap.", heading.DescriptionEn);
        Assert.Equal("7404", nodes.Single(n => n.Code == "7404.00.00").ParentCode);
        Assert.Contains(issues, i => i.Type == "HEADING_FROM_LINE" && i.Severity == "INFO");
    }

    [Fact]
    public void Dashed_line_without_a_heading_row_is_reported_not_invented()
    {
        var (nodes, issues, _) = Build(Row(BtkiRowKind.Line, "7410.11.10", "- - - Palutan tembaga"));
        Assert.DoesNotContain(nodes, n => n.Code == "7410");
        Assert.Contains(issues, i => i.Type == "MISSING_HEADING_ROW");
    }

    [Fact]
    public void Duplicate_code_keeps_the_first_row_and_warns()
    {
        var (nodes, issues, _) = Build(
            Row(BtkiRowKind.Heading, "7404", "Sisa dan skrap tembaga."),
            Row(BtkiRowKind.Line, "7404.00.00", "Sisa dan skrap tembaga.", seq: 2),
            Row(BtkiRowKind.Line, "7404.00.00", "Teks lain", seq: 3));

        Assert.Single(nodes, n => n.Code == "7404.00.00");
        Assert.Equal("Sisa dan skrap tembaga.", nodes.Single(n => n.Code == "7404.00.00").DescriptionId);
        Assert.Contains(issues, i => i.Type == "DUPLICATE_CODE" && i.Severity == "WARNING");
    }

    [Fact]
    public void Group_text_missing_from_the_pdf_is_listed_for_later_filling()
    {
        var (nodes, issues, missing) = Build(
            Row(BtkiRowKind.Heading, "7409", "Pelat tembaga."),
            Row(BtkiRowKind.Line, "7409.11.00", "- - Dalam gulungan", seq: 2),
            Row(BtkiRowKind.Line, "7409.19.00", "- - Lain-lain", seq: 3));

        Assert.Equal(["7409.1"], missing);
        Assert.Equal("7409", nodes.Single(n => n.Code == "7409.11.00").ParentCode);
        Assert.Contains(issues, i => i.Type == "MISSING_GROUP_TEXT");
    }

    [Fact]
    public void Two_dash_subheading_without_its_group_row_asks_for_the_group_text()
    {
        var (nodes, _, missing) = Build(
            Row(BtkiRowKind.Heading, "1602", "Olahan daging."),
            Row(BtkiRowKind.Subheading, "1602.49", "- - Lain-lain, termasuk campuran :", seq: 2),
            Row(BtkiRowKind.Line, "1602.49.11", "- - - Dalam kemasan", seq: 3));

        Assert.Equal(["1602.4"], missing);
        Assert.Equal("1602.49", nodes.Single(n => n.Code == "1602.49.11").ParentCode);
    }

    [Fact]
    public void Uncoded_row_deeper_than_one_dash_is_not_stored_but_reported()
    {
        var (nodes, issues, _) = Build(
            Row(BtkiRowKind.Heading, "8501", "Motor listrik."),
            Row(BtkiRowKind.Group, null, "- - Lain-lain :", seq: 2),
            Row(BtkiRowKind.Line, "8501.10.00", "- Motor", seq: 3));

        Assert.Equal(2, nodes.Count);
        Assert.Contains(issues, i => i.Type == "UNCODED_INTERMEDIATE");
    }

    [Fact]
    public void Built_nodes_pass_the_existing_structure_dry_run_without_errors_or_ambiguity()
    {
        var (nodes, _, _) = Build(
            Row(BtkiRowKind.Heading, "8429", "Buldoser, ekskavator."),
            Row(BtkiRowKind.Group, null, "- Sekop mekanik :", seq: 2),
            Row(BtkiRowKind.Line, "8429.51.00", "- - Shovel loader", seq: 3),
            Row(BtkiRowKind.Line, "8429.52.00", "- - Mesin dengan superstruktur", seq: 4),
            Row(BtkiRowKind.Heading, "7404", "Sisa dan skrap tembaga.", seq: 5),
            Row(BtkiRowKind.Line, "7404.00.00", "Sisa dan skrap tembaga.", seq: 6));

        var dry = BtkiStructureService.DryRun(nodes);
        Assert.Equal(0, dry.ErrorCount);
        Assert.Equal(0, dry.AmbiguousNodes);
        Assert.True(dry.CanCommit);
    }
}
