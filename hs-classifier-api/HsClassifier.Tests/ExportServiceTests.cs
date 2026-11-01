using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace HsClassifier.Tests;

public class ExportServiceTests
{
    private const string Code = "8517.13.00";

    private static async Task<(ExportService Svc, ClassificationCase Case)> Setup(
        CaseStatus status = CaseStatus.Determined, string? finalCode = Code)
    {
        var db = new HsClassifierDbContext(AiTest.NewOptions());
        var v = new NomenclatureVersion { Name = "BTKI 2022", EffectiveFrom = new DateTime(2022, 4, 1),
            State = ImportState.Active, IsActive = true };
        db.NomenclatureVersions.Add(v);
        var line = new BtkiTariffLine { VersionId = v.Id, Code = Code, HeadingCode = "8517", IsActive = true,
            DescriptionId = "Telepon pintar (smartphone)" };
        db.BtkiTariffLines.Add(line);
        await db.SaveChangesAsync();
        db.MfnRates.Add(new MfnRate { TariffLineId = line.Id, VersionId = v.Id, RateValue = 5, SourceId = "btki:8517.13.00:mfn" });
        db.FtaRates.Add(new FtaRate { TariffLineId = line.Id, VersionId = v.Id, SchemeCode = "ATIGA", PartnerCountryCode = "ASEAN", RateValue = 0 });
        db.TaxRates.Add(new TaxRate { TariffLineId = line.Id, VersionId = v.Id, TaxType = TaxType.Ppn, RateValue = 11 });
        db.TaxRates.Add(new TaxRate { TariffLineId = line.Id, VersionId = v.Id, TaxType = TaxType.Pph22, RateValue = 7.5m });

        var c = new ClassificationCase
        {
            UserId = 7, VersionId = v.Id, Status = status, FinalCode = finalCode,
            FinalDescription = finalCode is null ? null : "Telepon pintar (smartphone)",
            Confidence = ConfidenceLevel.High, ConfidenceScore = 85,
            ConfidenceJson = """{"Components":[{"Name":"Kelengkapan fakta penentu","Weight":25,"Points":25}],"Caps":[],"RiskFlags":["AI_TERM_SUGGESTION"]}""",
            FactsJson = """{"generic_name":"telepon pintar","material":"","ai_suggestion":"{\"terms\":[\"x\"]}"}""",
            CandidateCodesJson = """["8517"]""",
        };
        db.ClassificationCases.Add(c);
        await db.SaveChangesAsync();
        db.ReasoningEvents.Add(new ReasoningEvent { CaseId = c.Id, EventOrder = 1, RuleApplied = "KUM_HS_1",
            Decision = "Retained", Reasoning = "Uraian pos cocok dengan fungsi barang.", SourceIdsJson = """["btki:8517"]""" });
        db.ExpertCorrections.Add(new ExpertCorrection { CaseId = c.Id, Action = "EXPERT_APPROVED", AfterCode = Code,
            Reason = "Sesuai uraian resmi", ReviewerId = 3 });
        await db.SaveChangesAsync();
        var svc = new ExportService(db, new TariffLookupService(db, new VersionManager(db)));
        return (svc, c);
    }

    [Fact]
    public async Task Model_carries_code_description_tariffs_and_hides_internal_facts()
    {
        var (svc, c) = await Setup();
        var m = await svc.BuildAsync(c);

        Assert.True(m.IsFinal);
        Assert.Equal(Code, m.FinalCode);
        Assert.Equal("Telepon pintar (smartphone)", m.FinalDescription);
        Assert.Equal("BTKI 2022", m.VersionName);
        Assert.Equal(5, m.Tariff!.Mfn!.Value);
        Assert.Equal(11, m.Tariff.Ppn!.Value);
        Assert.Contains("Rules of Origin", m.Tariff.FtaDisclaimer);
        Assert.Equal("telepon pintar", m.Facts["generic_name"]);
        Assert.DoesNotContain("ai_suggestion", m.Facts.Keys);   // internal cache, not a goods fact
        Assert.DoesNotContain("material", m.Facts.Keys);        // empty facts are left out
        Assert.Single(m.Reasoning);
        Assert.Equal("btki:8517", m.Reasoning[0].SourceIds[0]);
        Assert.Single(m.Corrections);
        Assert.Contains("AI_TERM_SUGGESTION", m.ConfidenceDetail!.RiskFlags);
        Assert.Null(m.Notice);
    }

    [Fact]
    public async Task Case_without_final_code_is_marked_not_final_and_has_no_tariff()
    {
        var (svc, c) = await Setup(CaseStatus.NeedExpertReview, finalCode: null);
        var m = await svc.BuildAsync(c);

        Assert.False(m.IsFinal);
        Assert.Null(m.Tariff);
        Assert.Contains("Bukan keputusan final", m.Notice);
    }

    [Fact]
    public async Task Json_uses_camel_case_and_includes_the_disclaimers()
    {
        var (svc, c) = await Setup();
        var file = svc.Render(await svc.BuildAsync(c), "json");

        Assert.Equal("application/json", file.ContentType);
        Assert.Equal("json", file.Extension);
        using var doc = JsonDocument.Parse(file.Bytes);
        Assert.Equal(Code, doc.RootElement.GetProperty("finalCode").GetString());
        Assert.Contains("Rules of Origin", doc.RootElement.GetProperty("tariff").GetProperty("ftaDisclaimer").GetString());
    }

    [Fact]
    public async Task Docx_opens_and_contains_code_description_and_disclaimers()
    {
        var (svc, c) = await Setup();
        var file = svc.Render(await svc.BuildAsync(c), "docx");

        Assert.Equal("docx", file.Extension);
        using var ms = new MemoryStream(file.Bytes);
        using var word = WordprocessingDocument.Open(ms, false);
        var text = word.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Contains(Code, text);
        Assert.Contains("Telepon pintar (smartphone)", text);
        Assert.Contains("Rules of Origin", text);
        Assert.Contains("PMK", text);
        Assert.Contains("KUM_HS_1", text);
    }

    [Fact]
    public async Task Pdf_is_a_real_pdf_with_the_code_in_its_text()
    {
        var (svc, c) = await Setup();
        var file = svc.Render(await svc.BuildAsync(c), "pdf");

        Assert.Equal("application/pdf", file.ContentType);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(file.Bytes, 0, 5));
        using var pdf = PdfDocument.Open(file.Bytes);
        var text = string.Join(" ", pdf.GetPages().Select(p => p.Text));
        Assert.Contains(Code, text);
    }

    [Fact]
    public async Task Unknown_format_is_rejected()
    {
        var (svc, c) = await Setup();
        Assert.Throws<ArgumentException>(() => svc.Render(svc.BuildAsync(c).Result, "xlsx"));
    }
}
