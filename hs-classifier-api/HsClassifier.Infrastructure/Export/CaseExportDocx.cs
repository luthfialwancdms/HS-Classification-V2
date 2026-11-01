using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace HsClassifier.Infrastructure.Services;

/// <summary>Writes the report sections as a plain Word document (headings, tables, notes).</summary>
internal static class CaseExportDocx
{
    public static byte[] Render(CaseExportModel m)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body());
            var body = main.Document.Body!;

            body.Append(Para("Laporan Klasifikasi HS", bold: true, size: 36));
            foreach (var s in m.Sections())
            {
                body.Append(Para(s.Title, bold: true, size: 26, before: 240));
                if (s.Rows.Count > 0) body.Append(Table(s));
                if (s.Note is not null) body.Append(Para(s.Note, italic: true, size: 20, before: 80));
            }
            body.Append(Para(CaseExportModel.Footer, italic: true, size: 18, before: 360));
            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static W.Paragraph Para(string text, bool bold = false, bool italic = false, int size = 22, int before = 0)
    {
        var props = new W.RunProperties(new W.FontSize { Val = size.ToString() });
        if (bold) props.Append(new W.Bold());
        if (italic) props.Append(new W.Italic());
        return new W.Paragraph(
            new W.ParagraphProperties(new W.SpacingBetweenLines { Before = before.ToString(), After = "60" }),
            new W.Run(props, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static W.Table Table(ExportSection s)
    {
        var border = (W.BorderValues)W.BorderValues.Single;
        var table = new W.Table(new W.TableProperties(
            new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" },
            new W.TableBorders(
                new W.TopBorder { Val = border, Size = 4 }, new W.BottomBorder { Val = border, Size = 4 },
                new W.LeftBorder { Val = border, Size = 4 }, new W.RightBorder { Val = border, Size = 4 },
                new W.InsideHorizontalBorder { Val = border, Size = 4 }, new W.InsideVerticalBorder { Val = border, Size = 4 })));

        if (s.Header.Length > 0) table.Append(Row(s.Header, header: true));
        foreach (var r in s.Rows)
            table.Append(Row(r, header: false, boldFirst: s.Header.Length == 0));
        return table;
    }

    private static W.TableRow Row(string[] cells, bool header, bool boldFirst = false)
    {
        var row = new W.TableRow();
        for (var i = 0; i < cells.Length; i++)
        {
            var run = new W.Run(new W.Text(cells[i]) { Space = SpaceProcessingModeValues.Preserve });
            if (header || (boldFirst && i == 0)) run.PrependChild(new W.RunProperties(new W.Bold()));
            var cell = new W.TableCell(new W.Paragraph(run));
            if (header) cell.TableCellProperties = new W.TableCellProperties(new W.Shading { Val = W.ShadingPatternValues.Clear, Fill = "E5E7EB" });
            row.Append(cell);
        }
        return row;
    }
}
