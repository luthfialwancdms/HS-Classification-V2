using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// Writes the report sections as a PDF with QuestPDF.
/// LICENSE: QuestPDF Community is free only for individuals, non-profits, open source, and organisations under
/// USD 1M annual gross revenue; public-sector entities and listed companies are not eligible. Anyone deploying
/// this must confirm they qualify or buy a commercial license (https://www.questpdf.com/license/).
/// </summary>
internal static class CaseExportPdf
{
    static CaseExportPdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(CaseExportModel m) => Document.Create(doc => doc.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(t => t.FontSize(9.5f));
        page.Header().Text("Laporan Klasifikasi HS").FontSize(18).Bold();

        page.Content().PaddingVertical(8).Column(col =>
        {
            col.Spacing(10);
            foreach (var s in m.Sections())
            {
                col.Item().Text(s.Title).FontSize(12).Bold();
                if (s.Rows.Count > 0) col.Item().Element(c => Table(c, s));
                if (s.Note is not null) col.Item().Text(s.Note).Italic().FontSize(9);
            }
            col.Item().PaddingTop(12).Text(CaseExportModel.Footer).Italic().FontSize(8);
        });

        page.Footer().AlignCenter().Text(t =>
        {
            t.Span("Halaman ");
            t.CurrentPageNumber();
            t.Span(" dari ");
            t.TotalPages();
        });
    })).GeneratePdf();

    private static void Table(IContainer container, ExportSection s)
    {
        var cols = s.Header.Length > 0 ? s.Header.Length : 2;
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                if (s.Header.Length == 0) { c.ConstantColumn(130); c.RelativeColumn(); }
                else for (var i = 0; i < cols; i++) c.RelativeColumn(s.Header[i] is "Alasan" ? 3 : 1);
            });

            if (s.Header.Length > 0)
                table.Header(h =>
                {
                    foreach (var text in s.Header)
                        h.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4).Text(text).Bold();
                });

            foreach (var row in s.Rows)
                for (var i = 0; i < cols; i++)
                {
                    var t = table.Cell().Border(0.5f).BorderColor(Colors.Grey.Medium).Padding(4).Text(i < row.Length ? row[i] : "");
                    if (s.Header.Length == 0 && i == 0) t.Bold();
                }
        });
    }
}
