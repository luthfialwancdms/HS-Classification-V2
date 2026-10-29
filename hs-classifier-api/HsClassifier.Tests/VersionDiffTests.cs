using HsClassifier.Application.Services;

namespace HsClassifier.Tests;

public class VersionDiffTests
{
    private static LineText L(string code, string text) => new(code, text, null);

    [Fact]
    public void Added_removed_changed_and_ocr_only_are_told_apart()
    {
        var before = new[]
        {
            L("7404", "Sisa dan skrap tembaga."),
            L("7404.00.00", "- Lain-Iain"),                                        // OCR: capital I for l
            L("7409.11.00", "- - Dari jenis yang digunakan untuk kendaraan"),
            L("7499.00.00", "Hanya di versi lama"),
            L("8415.81.21", "- - - - Dengan kapasitas pendinginan tidak melebihi 26.38 kW"),
        };
        var after = new[]
        {
            L("7404", "Sisa  dan skrap tembaga"),                                   // spacing and a full stop only
            L("7404.00.00", "- Lain-lain"),
            L("7409.11.00", "- - Dari jenis yang digunakan pada kendaraan"),        // a real rewording
            L("7410.10.00", "Baru di PDF"),
            L("8415.81.21", "- - - - Dengan kapasitas pendinginan tidak melebihi 26,38 kW"),   // decimal comma
        };

        var d = VersionDiff.Compare(before, after);

        Assert.Equal(["7410.10.00"], d.Added.Select(e => e.Code));
        Assert.Equal(["7499.00.00"], d.Removed.Select(e => e.Code));
        Assert.Equal(["7409.11.00"], d.Changed.Select(e => e.Code));
        Assert.Equal(["7404.00.00"], d.OcrOnly.Select(e => e.Code));
        Assert.Equal(2, d.Unchanged);                  // 7404 and 8415.81.21
        Assert.Equal((5, 5), (d.BaselineCount, d.CandidateCount));
        Assert.Equal("pos tarif", d.Added[0].Level);
        Assert.Equal("74", d.Added[0].Chapter);
    }

    [Fact]
    public void Number_changes_are_real_changes_not_ocr_noise()
    {
        var d = VersionDiff.Compare([L("8501.20.11", "- - - Dengan keluaran tidak melebihi 1 kW")],
                                    [L("8501.20.11", "- - - Dengan keluaran tidak melebihi 10 kW")]);
        Assert.Single(d.Changed);
        Assert.Empty(d.OcrOnly);
    }

    [Fact]
    public void Duplicate_codes_in_one_version_count_once()
    {
        var d = VersionDiff.Compare([L("8504.50.20", "a"), L("8504.50.20", "b")], [L("8504.50.20", "a")]);
        Assert.Equal(1, d.BaselineCount);
        Assert.Equal(1, d.Unchanged);
    }
}
