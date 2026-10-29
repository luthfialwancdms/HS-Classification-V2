using HsClassifier.Application.Interfaces;

namespace HsClassifier.Application.Services;

/// <summary>
/// Rows of the BTKI PDF table → tariff nodes in the shape the classifier already reads:
/// 4-char heading ("7404"), 6-char one-dash group ("7409.1"), 7-char subheading ("7407.10"), 10-char line ("7407.10.30").
/// A line's parent is its subheading, else its group, else its heading. Nothing is guessed:
/// missing or odd rows become issues, and a duplicate code keeps the first occurrence.
/// </summary>
public static class BtkiHierarchyBuilder
{
    // chapter → Roman section number
    private static readonly (int From, int To, string Section)[] Sections =
    [
        (1, 5, "I"), (6, 14, "II"), (15, 15, "III"), (16, 24, "IV"), (25, 27, "V"), (28, 38, "VI"), (39, 40, "VII"),
        (41, 43, "VIII"), (44, 46, "IX"), (47, 49, "X"), (50, 63, "XI"), (64, 67, "XII"), (68, 70, "XIII"), (71, 71, "XIV"),
        (72, 83, "XV"), (84, 85, "XVI"), (86, 89, "XVII"), (90, 92, "XVIII"), (93, 93, "XIX"), (94, 96, "XX"), (97, 99, "XXI"),
    ];

    public static string? SectionOf(string chapter) =>
        int.TryParse(chapter, out var c) ? Sections.FirstOrDefault(s => c >= s.From && c <= s.To).Section : null;

    public static List<ParsedTariffNode> Build(IReadOnlyList<BtkiTableRow> rows, string source,
        List<ParseIssue> issues, List<string>? missingGroups = null)
    {
        var nodes = new List<ParsedTariffNode>();
        var byCode = new Dictionary<string, ParsedTariffNode>();
        var headingFromLine = new List<string>();
        var intermediate = new List<string>();
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        string? heading = null;

        void Add(string code, string? parent, BtkiTableRow r, int rowNo, string sourceId, string? textId = null, string? textEn = null)
        {
            if (byCode.ContainsKey(code))
            {
                issues.Add(new ParseIssue
                {
                    Severity = "WARNING", Type = "DUPLICATE_CODE", Code = code, RowNumber = rowNo,
                    Message = $"Kode {code} muncul lagi di halaman {r.Page}; baris pertama yang dipakai.",
                });
                return;
            }
            var chapter = code[..2];
            var text = (textId ?? r.TextId).Trim();
            var en = (textEn ?? r.TextEn).Trim();
            var node = new ParsedTariffNode
            {
                Code = code, ParentCode = parent,
                SectionCode = SectionOf(chapter), ChapterCode = chapter, HeadingCode = code[..4],
                SubheadingCode = code.Length >= 7 ? code[..7] : null,
                IndentLevel = code.Length == 4 ? 0 : Math.Max(1, r.Depth),
                DescriptionId = text, DescriptionEn = en.Length == 0 ? null : en,
                Status = "A", SourceId = sourceId,
            };
            nodes.Add(node);
            byCode[code] = node;
            if (code.Length >= 4 && text.Length == 0)
                issues.Add(new ParseIssue
                {
                    Severity = "WARNING", Type = "EMPTY_DESCRIPTION", Code = code, RowNumber = rowNo,
                    Message = $"Halaman {r.Page}: uraian Indonesia kosong untuk {code}.",
                });
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var rowNo = i + 1;
            var src = $"pdf:{source}:p{r.Page}:row_{r.Seq}";

            switch (r.Kind)
            {
                case BtkiRowKind.Heading:
                    heading = r.Code;
                    Add(r.Code!, null, r, rowNo, src);
                    break;

                case BtkiRowKind.Group when r.Code is not null:
                    Add(r.Code!, heading ?? r.Code![..4], r, rowNo, src);
                    break;

                case BtkiRowKind.Group:
                    if (r.Depth != 1)
                    {
                        intermediate.Add($"p{r.Page}: {r.TextId}");
                        break;
                    }
                    // A one-dash group has no code in the PDF: it is "<heading>.<first digit of the 6-digit subheading>"
                    var next = rows.Skip(i + 1).FirstOrDefault(x => x.Code is { Length: >= 7 });
                    if (next is null || heading is null || next.Code![..4] != heading)
                    {
                        issues.Add(new ParseIssue
                        {
                            Severity = "WARNING", Type = "UNCODED_GROUP_DROPPED", RowNumber = rowNo,
                            Message = $"Halaman {r.Page}: grup \"{r.TextId}\" tidak punya baris berkode sesudahnya di pos yang sama; dilewati.",
                        });
                        break;
                    }
                    Add(next.Code[..6], heading, r, rowNo, src);
                    break;

                case BtkiRowKind.Subheading:
                    heading ??= r.Code![..4];
                    Add(r.Code!, heading, r, rowNo, src);
                    // "- - Lain-lain :" (two dashes) hangs under a one-dash group that the PDF may not print
                    if (r.Depth >= 2) missing.Add(r.Code![..6]);
                    break;

                case BtkiRowKind.Line:
                    var h4 = r.Code![..4];
                    if (!byCode.ContainsKey(h4))
                    {
                        // The PDF prints a single-line heading as just its tariff line: no dash, no separate "74.04" row
                        if (r.Depth == 0)
                        {
                            Add(h4, null, r, rowNo, src + ":heading");
                            headingFromLine.Add(h4);
                        }
                        else
                            issues.Add(new ParseIssue
                            {
                                Severity = "WARNING", Type = "MISSING_HEADING_ROW", Code = r.Code, RowNumber = rowNo,
                                Message = $"Halaman {r.Page}: pos {h4} tidak punya baris pos (4 digit) di PDF.",
                            });
                    }
                    heading = h4;

                    var sub = r.Code![..7];
                    var group = r.Code[..6];
                    string parent;
                    if (byCode.ContainsKey(sub)) parent = sub;
                    else if (byCode.ContainsKey(group)) parent = group;
                    else
                    {
                        parent = h4;
                        if (r.Depth >= 2) missing.Add(group);
                    }
                    Add(r.Code, parent, r, rowNo, src);
                    break;
            }
        }

        if (headingFromLine.Count > 0)
            issues.Add(new ParseIssue
            {
                Severity = "INFO", Type = "HEADING_FROM_LINE", RowNumber = 0,
                Message = $"{headingFromLine.Count} pos hanya tercetak sebagai satu baris tarif; baris pos 4 digit dibuat dari uraian baris itu " +
                          $"(mis. {string.Join(", ", headingFromLine.Take(8))}).",
            });
        if (intermediate.Count > 0)
            issues.Add(new ParseIssue
            {
                Severity = "INFO", Type = "UNCODED_INTERMEDIATE", RowNumber = 0,
                Message = $"{intermediate.Count} baris perantara tanpa kode (dua strip atau lebih) tidak disimpan sebagai node " +
                          $"(mis. {string.Join("; ", intermediate.Take(3))}).",
            });
        if (missing.Count > 0)
            issues.Add(new ParseIssue
            {
                Severity = "INFO", Type = "MISSING_GROUP_TEXT", RowNumber = 0,
                Message = $"{missing.Count} grup induk tidak tercetak di PDF (mis. {string.Join(", ", missing.Take(8))}); " +
                          "teksnya dapat diambil dari versi aktif saat commit.",
            });
        missingGroups?.AddRange(missing.Where(g => !byCode.ContainsKey(g)));
        return nodes;
    }
}
