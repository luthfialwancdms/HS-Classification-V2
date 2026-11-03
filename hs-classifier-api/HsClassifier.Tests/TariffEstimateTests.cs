using System.Reflection;
using System.Text;
using HsClassifier.API.Controllers;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HsClassifier.Tests;

/// <summary>AI estimate of PPN, PPh 22 and FTA scheme names: off by default, fills only what the database lacks.</summary>
public class TariffEstimateTests
{
    private const string Code = "8517.13.00";

    private const string GoodAnswer =
        """{"ppn":{"percent":11,"basis":"UU HPP"},"pph22":{"percent":2.5,"basis":"PMK 41/2022"},"fta_schemes":["ATIGA","ACFTA"]}""";

    private sealed class Rig
    {
        public required HsClassifierDbContext Db { get; init; }
        public required FakeProvider Ai { get; init; }
        public required CapturingRecorder Recorder { get; init; }
        public required TariffEstimateService Estimates { get; init; }
        public required TariffLookupService Lookup { get; init; }
    }

    /// <param name="ppn">Official PPN already in the database, or null.</param>
    private static async Task<Rig> Setup(decimal? ppn = null, decimal? pph22 = null, bool fta = false, string answer = GoodAnswer)
    {
        var name = Guid.NewGuid().ToString();
        var db = new HsClassifierDbContext(AiTest.NewOptions(name));
        var v = new NomenclatureVersion { Name = "BTKI 2022", EffectiveFrom = new DateTime(2022, 4, 1), State = ImportState.Active, IsActive = true };
        db.NomenclatureVersions.Add(v);
        var line = new BtkiTariffLine { VersionId = v.Id, Code = Code, HeadingCode = "8517", IsActive = true, DescriptionId = "Telepon pintar (smartphone)" };
        db.BtkiTariffLines.Add(line);
        await db.SaveChangesAsync();
        db.MfnRates.Add(new MfnRate { TariffLineId = line.Id, VersionId = v.Id, RateValue = 5 });
        if (ppn is { } p) db.TaxRates.Add(new TaxRate { TariffLineId = line.Id, VersionId = v.Id, TaxType = TaxType.Ppn, RateValue = p });
        if (pph22 is { } h) db.TaxRates.Add(new TaxRate { TariffLineId = line.Id, VersionId = v.Id, TaxType = TaxType.Pph22, RateValue = h });
        if (fta) db.FtaRates.Add(new FtaRate { TariffLineId = line.Id, VersionId = v.Id, SchemeCode = "ATIGA", PartnerCountryCode = "ASEAN", RateValue = 0 });
        await db.SaveChangesAsync();

        var ai = new FakeProvider { OnChat = () => Task.FromResult(new AiChatResult(answer, false, new("openai-compatible", "api.groq.com", 200, null, 30, 10, 40))) };
        var rec = new CapturingRecorder();
        var sp = AiTest.Services(name);
        var est = new TariffEstimateService(db, sp.GetRequiredService<IServiceScopeFactory>(), ai, rec, TimeProvider.System,
            NullLogger<TariffEstimateService>.Instance);
        return new Rig { Db = db, Ai = ai, Recorder = rec, Estimates = est, Lookup = new TariffLookupService(db, new VersionManager(db), est) };
    }

    [Fact]
    public async Task Off_by_default_no_ai_request_and_no_estimate()
    {
        var r = await Setup();
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Null(t.AiEstimate);
        Assert.Null(t.Ppn);
        Assert.Equal(0, r.Ai.ChatCalls);
    }

    [Fact]
    public async Task On_fills_missing_ppn_pph22_and_fta_names_with_the_estimate_label()
    {
        var r = await Setup();
        await r.Estimates.SetEnabledAsync(true, "admin@x");
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Null(t.Ppn);                                  // the official fields stay empty
        Assert.Null(t.Pph22);
        var e = t.AiEstimate!;
        Assert.Equal("Estimasi AI, belum terverifikasi", e.Label);
        Assert.Equal(11m, e.Ppn!.Percent);
        Assert.Equal(2.5m, e.Pph22!.Percent);
        Assert.Equal(["ATIGA", "ACFTA"], e.FtaSchemes);
        Assert.Equal(1, r.Ai.ChatCalls);
        Assert.Contains(Code, r.Ai.LastChatUser);
        Assert.Contains("Telepon pintar", r.Ai.LastChatUser);
        Assert.DoesNotContain("userId", r.Ai.LastChatUser!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Official_values_are_never_overridden_and_only_missing_fields_are_estimated()
    {
        var r = await Setup(ppn: 10m);
        await r.Estimates.SetEnabledAsync(true, null);
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Equal(10m, t.Ppn!.Value);
        Assert.Null(t.AiEstimate!.Ppn);                      // AI said 11, but the database has 10
        Assert.NotNull(t.AiEstimate.Pph22);
    }

    [Fact]
    public async Task When_the_database_has_everything_the_ai_is_not_asked()
    {
        var r = await Setup(ppn: 11m, pph22: 2.5m, fta: true);
        await r.Estimates.SetEnabledAsync(true, null);
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Null(t.AiEstimate);
        Assert.Equal(0, r.Ai.ChatCalls);
    }

    [Fact]
    public async Task Answer_is_cached_per_code_until_an_admin_clears_it()
    {
        var r = await Setup();
        await r.Estimates.SetEnabledAsync(true, null);
        await r.Lookup.GetRatesAsync(Code);
        var second = await r.Lookup.GetRatesAsync(Code);
        Assert.Equal(1, r.Ai.ChatCalls);
        Assert.NotNull(second.AiEstimate);
        Assert.Equal(1, await r.Estimates.CachedCountAsync());

        Assert.Equal(1, await r.Estimates.ClearCacheAsync());
        await r.Lookup.GetRatesAsync(Code);
        Assert.Equal(2, r.Ai.ChatCalls);
    }

    [Fact]
    public async Task Turning_it_off_hides_the_estimate_again_without_touching_the_cache()
    {
        var r = await Setup();
        await r.Estimates.SetEnabledAsync(true, null);
        await r.Lookup.GetRatesAsync(Code);
        await r.Estimates.SetEnabledAsync(false, null);
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Null(t.AiEstimate);
        Assert.False(await r.Estimates.IsEnabledAsync());
        Assert.Equal(1, await r.Estimates.CachedCountAsync());
    }

    [Fact]
    public async Task A_success_is_logged_as_a_tariff_estimate_request()
    {
        var r = await Setup();
        await r.Estimates.SetEnabledAsync(true, null);
        await r.Lookup.GetRatesAsync(Code);
        var log = Assert.Single(r.Recorder.Entries);
        Assert.Equal((AiCallKind.TariffEstimate, AiCallStatus.Success, 40), (log.Kind, log.Status, log.TotalTokens));
    }

    [Theory]
    [InlineData("Kuota harian habis, coba lagi besok.")]       // proxy notice with HTTP 200
    [InlineData("")]
    [InlineData("{ not json }")]
    public async Task Unreadable_answer_is_a_failed_call_and_gives_no_estimate_and_is_not_cached(string answer)
    {
        var r = await Setup(answer: answer);
        await r.Estimates.SetEnabledAsync(true, null);
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Null(t.AiEstimate);
        var log = Assert.Single(r.Recorder.Entries);
        Assert.Equal((AiCallStatus.Failed, AiCall.Parse), (log.Status, log.ErrorCode));
        Assert.Equal(0, await r.Estimates.CachedCountAsync());
    }

    [Fact]
    public async Task Provider_error_gives_no_estimate_and_a_failed_log()
    {
        var r = await Setup();
        r.Ai.OnChat = () => Task.FromResult(new AiChatResult(null, true, new("openai-compatible", "h", 503, "HTTP_503", null, null, null)));
        await r.Estimates.SetEnabledAsync(true, null);
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Null(t.AiEstimate);
        Assert.Equal("HTTP_503", Assert.Single(r.Recorder.Entries).ErrorCode);
    }

    [Fact]
    public async Task Missing_key_is_not_a_usage_event()
    {
        var r = await Setup();
        r.Ai.OnChat = () => Task.FromResult(new AiChatResult(null, true, new("openai-compatible", "h", null, AiCall.NoKey, null, null, null)));
        await r.Estimates.SetEnabledAsync(true, null);
        Assert.Null((await r.Lookup.GetRatesAsync(Code)).AiEstimate);
        Assert.Empty(r.Recorder.Entries);
    }

    [Fact]
    public async Task A_broken_usage_log_or_provider_never_breaks_the_tariff_lookup()
    {
        var r = await Setup();
        r.Recorder.Throw = true;
        await r.Estimates.SetEnabledAsync(true, null);
        Assert.NotNull((await r.Lookup.GetRatesAsync(Code)).AiEstimate);
        r.Ai.OnChat = () => throw new InvalidOperationException("boom");
        await r.Estimates.ClearCacheAsync();
        var t = await r.Lookup.GetRatesAsync(Code);
        Assert.Null(t.AiEstimate);
        Assert.Equal(5, t.Mfn!.Value);
    }

    [Fact]
    public void Parse_drops_implausible_numbers_odd_scheme_names_and_caps_the_list()
    {
        var (p, bad) = TariffEstimateService.Parse(
            """sure: {"ppn":{"percent":"11%","basis":"UU HPP"},"pph22":{"percent":80},"fta_schemes":["ATIGA","<script>","a","ACFTA","ATIGA","B1","B2","B3","B4","B5","B6","B7"]}""");
        Assert.False(bad);
        Assert.Equal(11m, p!.Ppn!.Percent);
        Assert.Null(p.Pph22);                                 // 80% PPh 22 is not plausible
        Assert.Equal(8, p.FtaSchemes.Count);
        Assert.DoesNotContain("<script>", p.FtaSchemes);
        Assert.Single(p.FtaSchemes, s => s == "ATIGA");
    }

    [Fact]
    public void Parse_accepts_a_fenced_answer_and_flags_text_without_json()
    {
        Assert.False(TariffEstimateService.Parse("```json\n" + GoodAnswer + "\n```").Unreadable);
        Assert.True(TariffEstimateService.Parse("quota exhausted").Unreadable);
    }

    [Fact]
    public async Task Json_export_and_sections_carry_the_estimate_with_its_label()
    {
        var r = await Setup();
        await r.Estimates.SetEnabledAsync(true, null);
        var t = await r.Lookup.GetRatesAsync(Code);
        var model = new CaseExportModel { IsFinal = true, FinalCode = Code, Tariff = t };
        var section = Assert.Single(model.Sections(), s => s.Title.StartsWith("Estimasi AI"));
        Assert.Contains(section.Rows, row => row[0] == "PPN impor" && row[1] == "11%");
        Assert.Contains(section.Rows, row => row[1] == "ATIGA, ACFTA");
        Assert.Contains("Bukan data resmi", section.Note);
        var json = Encoding.UTF8.GetString(new ExportService(r.Db, r.Lookup).Render(model, "json").Bytes);
        Assert.Contains("Estimasi AI, belum terverifikasi", json);
    }

    [Fact]
    public async Task Without_an_estimate_the_export_has_no_estimate_section()
    {
        var r = await Setup();
        var model = new CaseExportModel { IsFinal = true, FinalCode = Code, Tariff = await r.Lookup.GetRatesAsync(Code) };
        Assert.DoesNotContain(model.Sections(), s => s.Title.StartsWith("Estimasi AI"));
    }

    [Fact]
    public void The_switch_endpoints_are_admin_only()
    {
        var t = typeof(AdminTariffEstimateController);
        var auth = Assert.Single(t.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(nameof(UserRole.Admin), auth.Roles);
        Assert.Null(t.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.All(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            m => Assert.Null(m.GetCustomAttribute<AllowAnonymousAttribute>()));
    }
}
