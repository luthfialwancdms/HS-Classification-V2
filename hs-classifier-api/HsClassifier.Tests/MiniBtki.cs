using HsClassifier.Application.Classification;
using HsClassifier.Domain.Classification;
using HsClassifier.Domain.Entities;
using HsClassifier.Domain.Enums;
using HsClassifier.Infrastructure.Persistence;
using HsClassifier.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HsClassifier.Tests;

/// <summary>An ITermSuggester that answers with a fixed suggestion (or nothing / unavailable).</summary>
internal sealed class FakeSuggester(TermSuggestion? suggestion = null, bool unavailable = false) : ITermSuggester
{
    public string Model => "test";
    public int Calls;
    public Task<SuggestionResult> SuggestAsync(FactSet facts, CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(new SuggestionResult(suggestion, unavailable));
    }
}

/// <summary>
/// A small BTKI (the headings the regression cases need, in the official wording style of PMK 26/2022) loaded into
/// EF InMemory, with the real classification pipeline on top. Not the full tariff: it shows how the engine behaves
/// on these goods, it does not replace a run against the real database.
/// ponytail: InMemory string matching is case-sensitive, SQL Server's default collation is not. Facts in these
/// tests use the same capitalisation as the fixture text.
/// </summary>
internal sealed class MiniBtki
{
    private static readonly (string Code, string Id, string En)[] Lines =
    [
        // Pumps (84.13 liquids, 84.14 air/vacuum/compressors)
        ("8413", "Pompa untuk cairan, dapat atau tidak dilengkapi dengan alat pengukur; elevator cairan.", "Pumps for liquids, whether or not fitted with a measuring device; liquid elevators."),
        ("8413.11.00", "- - Pompa untuk mendistribusikan bahan bakar atau pelumas, dari jenis yang digunakan di pompa bensin atau garasi", "- - Pumps for dispensing fuel or lubricants, of the type used in filling-stations or in garages"),
        ("8413.50.00", "- Pompa bolak-balik (reciprocating) lainnya", "- Other reciprocating positive displacement pumps"),
        ("8413.60.00", "- Pompa rotari (rotary) lainnya", "- Other rotary positive displacement pumps"),
        ("8413.70.00", "- Pompa sentrifugal lainnya", "- Other centrifugal pumps"),
        ("8413.81.00", "- - Pompa", "- - Pumps"),
        ("8414", "Pompa udara atau pompa vakum, kompresor udara atau gas lainnya dan kipas.", "Air or vacuum pumps, air or other gas compressors and fans."),
        ("8414.10.00", "- Pompa vakum", "- Vacuum pumps"),
        ("8414.30.00", "- Kompresor yang digunakan pada peralatan pendingin", "- Compressors of a kind used in refrigerating equipment"),
        ("8414.80.00", "- Lain-lain", "- Other"),
        // Computers and the things a laptop also contains
        ("8471", "Mesin pengolah data otomatis dan unitnya; pembaca magnetik atau optik, mesin untuk memindahkan data ke dalam media data dalam bentuk kode dan mesin untuk memproses data tersebut, tidak dirinci atau termasuk di tempat lain.", "Automatic data processing machines and units thereof; magnetic or optical readers, machines for transcribing data onto data media in coded form and machines for processing such data, not elsewhere specified or included."),
        ("8471.30", "- Mesin pengolah data otomatis portabel, berat tidak melebihi 10 kg, terdiri dari paling sedikit satu unit pemroses pusat, papan ketik dan layar :", "- Portable automatic data processing machines, weighing not more than 10 kg, consisting of at least a central processing unit, a keyboard and a display:"),
        ("8471.30.20", "- - Laptop termasuk notebook dan subnotebook", "- - Laptop including notebook and subnotebook"),
        ("8471.30.90", "- - Lain-lain", "- - Other"),
        ("8471.41.10", "- - - Komputer personal tidak termasuk komputer portabel dari subpos 8471.30", "- - - Personal computers excluding portable computers of subheading 8471.30"),
        ("8525", "Alat pemancar untuk radiotelepon, radiotelegraf, radiobroadcast atau televisi; kamera televisi, kamera digital dan perekam kamera video.", "Transmission apparatus for radio-broadcasting or television; television cameras, digital cameras and video camera recorders."),
        ("8525.80.10", "- Kamera digital", "- Digital cameras"),
        ("8518", "Mikrofon dan penyangganya; pengeras suara (loudspeaker), terpasang atau tidak terpasang dalam kotaknya.", "Microphones and stands therefor; loudspeakers, whether or not mounted in their enclosures."),
        ("8518.10.00", "- Mikrofon dan penyangganya", "- Microphones and stands therefor"),
        // Tractor vs dumper
        ("8701", "Traktor (selain kereta traktor dari pos 87.09).", "Tractors (other than tractors of heading 87.09)."),
        ("8701.20.00", "- Traktor jalan untuk semi-trailer", "- Road tractors for semi-trailers"),
        ("8701.91.00", "- - Traktor lainnya, dengan daya mesin tidak melebihi 18 kW", "- - Other tractors, of an engine power not exceeding 18 kW"),
        ("8704", "Kendaraan bermotor untuk pengangkutan barang.", "Motor vehicles for the transport of goods."),
        ("8704.10.00", "- Dumper yang dirancang untuk digunakan di luar jalan raya", "- Dumpers designed for off-highway use"),
        ("8704.21.00", "- - Dengan berat kendaraan total tidak melebihi 5 ton", "- - g.v.w. not exceeding 5 tonnes"),
        // Crane on a barge
        ("8426", "Derek kapal; crane, termasuk crane kabel; rangka angkat bergerak, kereta angkat dan kereta kerja yang dipasang crane.", "Ships' derricks; cranes, including cable cranes; mobile lifting frames, straddle carriers and works trucks fitted with a crane."),
        ("8426.30.00", "- Crane portal atau crane pedestal", "- Portal or pedestal jib cranes"),
        ("8426.91.00", "- - Dirancang untuk dipasang pada kendaraan jalan raya", "- - Designed for mounting on road vehicles"),
        ("8901", "Kapal pesiar, kapal feri, kapal kargo, tongkang (barge) dan kapal sejenis untuk pengangkutan orang atau barang.", "Cruise ships, excursion boats, ferry-boats, cargo ships, barges and similar vessels for the transport of persons or goods."),
        ("8901.90.10", "- Tongkang tanpa tenaga penggerak sendiri", "- Barges without self-propulsion"),
        ("8905", "Kapal api, kapal pengeruk, dok terapung, crane terapung dan kapal lain yang fungsi navigasinya hanya pelengkap terhadap fungsi utamanya.", "Light-vessels, fire-floats, dredgers, floating cranes and other vessels the navigability of which is subsidiary to their main function; floating docks."),
        ("8905.90.00", "- Lain-lain", "- Other"),
    ];

    public HsClassifierDbContext Db { get; }
    public ClassificationOrchestrator Orchestrator { get; }
    public ClassificationValidator Validator { get; }
    public CandidateGeneratorService Candidates { get; }

    private MiniBtki(HsClassifierDbContext db, ITermSuggester suggester)
    {
        Db = db;
        var vm = new VersionManager(db);
        Validator = new ClassificationValidator(db, vm);
        Candidates = new CandidateGeneratorService(db, vm);
        Orchestrator = new ClassificationOrchestrator(db, Candidates, Validator, new TariffLookupService(db, vm), suggester,
            new ConfigurationBuilder().Build());
    }

    public static async Task<MiniBtki> CreateAsync(ITermSuggester? suggester = null, bool inactive8518 = false)
    {
        var db = new HsClassifierDbContext(AiTest.NewOptions());
        var v = new NomenclatureVersion { Name = "BTKI 2022 (mini)", EffectiveFrom = new DateTime(2022, 4, 1),
            State = ImportState.Active, IsActive = true };
        db.NomenclatureVersions.Add(v);
        foreach (var (code, id, en) in Lines)
            db.BtkiTariffLines.Add(new BtkiTariffLine
            {
                VersionId = v.Id, Code = code, HeadingCode = code[..4], ChapterCode = code[..2],
                DescriptionId = id, DescriptionEn = en, SourceId = $"test:{code}",
                IsActive = !(inactive8518 && code.StartsWith("8518")),
            });
        await db.SaveChangesAsync();
        return new MiniBtki(db, suggester ?? new FakeSuggester());
    }

    /// <summary>Runs the whole pipeline for these facts.</summary>
    public Task<ClassificationSessionResult> ClassifyAsync(params (string, string)[] facts) =>
        Orchestrator.ClassifyAsync(1, facts.ToDictionary(f => f.Item1, f => (string?)f.Item2));

    /// <summary>Rules recorded for the case, in order, as "RULE:candidate:Decision".</summary>
    public async Task<List<string>> ChainAsync(long? caseId) =>
        (await Db.ReasoningEvents.Where(e => e.CaseId == caseId).OrderBy(e => e.EventOrder).ToListAsync())
            .Select(e => $"{e.RuleApplied}|{e.Decision}|{e.Reasoning}").ToList();
}
