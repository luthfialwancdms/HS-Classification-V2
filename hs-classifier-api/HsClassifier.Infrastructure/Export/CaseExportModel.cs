using System.Globalization;
using HsClassifier.Domain.Enums;

namespace HsClassifier.Infrastructure.Services;

public record ExportStep(int Order, string Rule, string Decision, string Reasoning, List<string> SourceIds);

public record ExportCorrection(string Action, string? BeforeCode, string? AfterCode, string? BeforeStatus,
    string? AfterStatus, string Reason, DateTime CreatedAt);

/// <summary>A titled block shared by the DOCX and PDF writers: either key/value rows or a table with a header.</summary>
public record ExportSection(string Title, string[] Header, List<string[]> Rows, string? Note = null);

public record ExportFile(byte[] Bytes, string ContentType, string Extension);

/// <summary>
/// Everything one exported report says about a case. Built once by <see cref="ExportService"/>; the JSON file is
/// this object as is, DOCX and PDF render <see cref="Sections"/>.
/// </summary>
public class CaseExportModel
{
    public const string Footer =
        "Dokumen ini dihasilkan otomatis dari database BTKI dan aturan KUM HS. Kode dan tarif yang ditampilkan " +
        "adalah rujukan, bukan penetapan resmi kepabeanan. Konsultasikan dengan ahli kepabeanan bila ragu.";

    public long CaseId { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsFinal { get; init; }
    /// <summary>Set when the case is not a final decision (no final code or still waiting for a person).</summary>
    public string? Notice { get; init; }
    public DateTime ClassificationDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public string? VersionName { get; init; }
    public string? FinalCode { get; init; }
    public string? FinalDescription { get; init; }
    public string? Confidence { get; init; }
    public int? ConfidenceScore { get; init; }
    public ConfidenceDetailDto? ConfidenceDetail { get; init; }
    public Dictionary<string, string> Facts { get; init; } = new();
    public List<string> CandidateCodes { get; init; } = new();
    public List<ExportStep> Reasoning { get; init; } = new();
    public List<ExportCorrection> Corrections { get; init; } = new();
    public TariffResult? Tariff { get; init; }

    public static string StatusLabel(CaseStatus s) => s switch
    {
        CaseStatus.Determined => "Ditentukan",
        CaseStatus.NeedClarification => "Perlu klarifikasi",
        CaseStatus.NeedExpertReview => "Menunggu review ahli",
        CaseStatus.Rejected => "Ditolak ahli",
        _ => s.ToString(),
    };

    public static string FormatRate(RateInfo r) =>
        string.Equals(r.Unit, "Percent", StringComparison.OrdinalIgnoreCase)
            ? $"{r.Value.ToString("0.####", CultureInfo.InvariantCulture)}%"
            : $"{r.Value.ToString("0.####", CultureInfo.InvariantCulture)} ({r.Unit})";

    private static string Dt(DateTime d) => d.ToString("dd MMM yyyy HH:mm", CultureInfo.GetCultureInfo("id-ID")) + " UTC";

    public List<ExportSection> Sections()
    {
        var s = new List<ExportSection>();
        var summary = new List<string[]>
        {
            new[] { "Nomor case", CaseId.ToString(CultureInfo.InvariantCulture) },
            new[] { "Status", Status },
            new[] { "Tanggal klasifikasi", Dt(ClassificationDate) },
            new[] { "Versi BTKI", VersionName ?? "-" },
            new[] { "Kode HS", FinalCode ?? "-" },
            new[] { "Uraian resmi BTKI", FinalDescription ?? "-" },
            new[] { "Tingkat keyakinan", Confidence is null ? "-" : $"{Confidence}{(ConfidenceScore is { } n ? $" ({n}/100)" : "")}" },
        };
        s.Add(new("Ringkasan", [], summary, Notice));

        if (Facts.Count > 0)
            s.Add(new("Fakta barang", [], Facts.Select(f => new[] { f.Key, f.Value }).ToList()));

        if (CandidateCodes.Count > 0)
            s.Add(new("Kandidat pos", ["Kode"], CandidateCodes.Select(c => new[] { c }).ToList()));

        if (Reasoning.Count > 0)
            s.Add(new("Dasar penetapan (rantai KUM HS)", ["No", "Aturan", "Keputusan", "Alasan", "Sumber"],
                Reasoning.Select(r => new[] { r.Order.ToString(CultureInfo.InvariantCulture), r.Rule, r.Decision, r.Reasoning,
                    string.Join(", ", r.SourceIds) }).ToList()));

        if (ConfidenceDetail is { } cd)
        {
            var notes = new List<string>();
            if (cd.Caps.Count > 0) notes.Add("Batas yang berlaku: " + string.Join("; ", cd.Caps));
            if (cd.RiskFlags.Count > 0) notes.Add("Penanda risiko: " + string.Join(", ", cd.RiskFlags));
            s.Add(new("Perhitungan keyakinan", ["Komponen", "Bobot", "Poin"],
                cd.Components.Select(c => new[] { c.Name, c.Weight.ToString("0.##", CultureInfo.InvariantCulture),
                    c.Points.ToString("0.#", CultureInfo.InvariantCulture) }).ToList(),
                notes.Count == 0 ? null : string.Join(" ", notes)));
        }

        if (IsFinal)
        {
            var rows = new List<string[]>();
            if (Tariff?.Mfn is { } mfn) rows.Add(["Bea Masuk MFN", FormatRate(mfn), mfn.SourceId ?? "-"]);
            foreach (var f in Tariff?.FtaRates ?? [])
                rows.Add([$"FTA {f.SchemeCode} ({f.PartnerCountry})", FormatRate(f), f.SourceId ?? "-"]);
            if (Tariff?.Ppn is { } ppn) rows.Add(["PPN impor", FormatRate(ppn), ppn.SourceId ?? "-"]);
            if (Tariff?.Pph22 is { } pph) rows.Add(["PPh Pasal 22 impor", FormatRate(pph), pph.SourceId ?? "-"]);
            s.Add(rows.Count == 0
                ? new("Tarif dari BTKI", [], [], "Tarif untuk kode ini tidak tersedia di database.")
                : new("Tarif dari BTKI", ["Jenis", "Tarif", "Sumber"], rows,
                    $"{Tariff!.FtaDisclaimer} {Tariff.TaxDisclaimer}"));

            if (Tariff?.AiEstimate is { } est)
            {
                var er = new List<string[]>();
                if (est.Ppn is { } ep) er.Add(["PPN impor", $"{ep.Percent.ToString("0.##", CultureInfo.InvariantCulture)}%", ep.Basis ?? "-"]);
                if (est.Pph22 is { } eh) er.Add(["PPh Pasal 22 impor", $"{eh.Percent.ToString("0.##", CultureInfo.InvariantCulture)}%", eh.Basis ?? "-"]);
                if (est.FtaSchemes.Count > 0) er.Add(["Skema FTA yang mungkin berlaku (tanpa tarif)", string.Join(", ", est.FtaSchemes), "-"]);
                s.Add(new("Estimasi AI (belum terverifikasi)", ["Jenis", "Estimasi", "Dasar"], er,
                    "Bukan data resmi BTKI. Diisi AI hanya untuk tarif yang tidak ada di database. Verifikasi sebelum dipakai."));
            }
        }

        if (Corrections.Count > 0)
            s.Add(new("Keputusan ahli", ["Waktu", "Aksi", "Kode (sebelum → sesudah)", "Alasan"],
                Corrections.Select(c => new[] { Dt(c.CreatedAt), c.Action, $"{c.BeforeCode ?? "-"} → {c.AfterCode ?? "-"}", c.Reason }).ToList()));

        return s;
    }
}
