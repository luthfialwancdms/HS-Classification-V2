using HsClassifier.Application.Interfaces;
using HsClassifier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HsClassifier.Infrastructure.Services;

public class TariffLookupService(HsClassifierDbContext db, IVersionManager versionManager)
{
    public const string FtaDisclaimer =
        "Tarif FTA yang ditampilkan adalah referensi ketersediaan. " +
        "Keberlakuan bergantung pada Rules of Origin, Certificate of Origin, dan persyaratan prosedural.";

    public const string TaxDisclaimer =
        "Tarif PPN dan PPh 22 adalah tarif standar dari BTKI. " +
        "Fasilitas pembebasan atau pengurangan berdasarkan PMK tertentu tidak dicakup.";

    public async Task<TariffResult> GetRatesAsync(string code, CancellationToken ct = default)
    {
        var result = new TariffResult { Code = code };
        var version = await versionManager.GetActiveVersionAsync(DateTime.UtcNow);
        if (version is null) return result;

        var line = await db.BtkiTariffLines
            .Where(t => t.VersionId == version.Id && t.Code == code)
            .Select(t => new { t.Id })
            .FirstOrDefaultAsync(ct);

        if (line is null) return result;

        // MFN rate (Task 15.1)
        var mfn = await db.MfnRates
            .Where(r => r.TariffLineId == line.Id && r.IsActive)
            .Select(r => new { r.RateValue, r.RateUnit, r.SourceId })
            .FirstOrDefaultAsync(ct);

        if (mfn is not null)
            result.Mfn = new RateInfo { Value = mfn.RateValue, Unit = mfn.RateUnit.ToString(), SourceId = mfn.SourceId };

        // FTA rates (Task 15.2)
        result.FtaRates = await db.FtaRates
            .Where(r => r.TariffLineId == line.Id && r.IsActive)
            .Select(r => new FtaRateInfo
            {
                SchemeCode = r.SchemeCode,
                PartnerCountry = r.PartnerCountryCode,
                Value = r.RateValue,
                Unit = r.RateUnit.ToString(),
                SourceId = r.SourceId
            })
            .ToListAsync(ct);
        result.FtaDisclaimer = FtaDisclaimer;

        // PPN + PPh22 (Task 15.3)
        var taxes = await db.TaxRates
            .Where(r => r.TariffLineId == line.Id && r.IsActive)
            .ToListAsync(ct);

        result.Ppn = taxes.Where(t => t.TaxType == Domain.Enums.TaxType.Ppn)
            .Select(t => new RateInfo { Value = t.RateValue, Unit = t.RateUnit.ToString(), SourceId = t.SourceId })
            .FirstOrDefault();
        result.Pph22 = taxes.Where(t => t.TaxType == Domain.Enums.TaxType.Pph22)
            .Select(t => new RateInfo { Value = t.RateValue, Unit = t.RateUnit.ToString(), SourceId = t.SourceId })
            .FirstOrDefault();
        result.TaxDisclaimer = TaxDisclaimer;

        return result;
    }
}

public class TariffResult
{
    public string Code { get; set; } = string.Empty;
    public RateInfo? Mfn { get; set; }
    public List<FtaRateInfo> FtaRates { get; set; } = new();
    public string FtaDisclaimer { get; set; } = string.Empty;
    public RateInfo? Ppn { get; set; }
    public RateInfo? Pph22 { get; set; }
    public string TaxDisclaimer { get; set; } = string.Empty;
}

public class RateInfo
{
    public decimal Value { get; set; }
    public string Unit { get; set; } = "PERCENT";
    public string? SourceId { get; set; }
}

public class FtaRateInfo : RateInfo
{
    public string SchemeCode { get; set; } = string.Empty;
    public string PartnerCountry { get; set; } = string.Empty;
}
